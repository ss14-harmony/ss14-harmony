using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

#nullable enable
namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(LegsMissingComponent))]
public sealed class LegsMissingForcedProneIntegrationTest
{
    private static EntityUid GetLeg(IEntityManager entityManager, EntityUid body, string category)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0), $"Body should have {category}");
        return ev.Parts[0];
    }

    [Test]
    public async Task BothLegsRemoved_KnockdownPersists_AfterStandAttempts_ReattachClears()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var standingState = entityManager.System<StandingStateSystem>();
        var stunSystem = entityManager.System<SharedStunSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid human = default;
        EntityUid legLeft = default;

        await server.WaitAssertion(() =>
        {
            human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var legRight = GetLeg(entityManager, human, "LegRight");
            legLeft = GetLeg(entityManager, human, "LegLeft");

            var removeRight = new OrganRemoveRequestEvent(legRight);
            entityManager.EventBus.RaiseLocalEvent(legRight, ref removeRight);
            Assert.That(removeRight.Success, Is.True, "Remove right leg should succeed");

            var removeLeft = new OrganRemoveRequestEvent(legLeft);
            entityManager.EventBus.RaiseLocalEvent(legLeft, ref removeLeft);
            Assert.That(removeLeft.Success, Is.True, "Remove left leg should succeed");
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.HasComponent<LegsMissingComponent>(human), Is.True,
                "Both legs missing should add LegsMissingComponent");
            Assert.That(entityManager.HasComponent<KnockedDownComponent>(human), Is.True,
                "Both legs missing should knock down");
            Assert.That(standingState.IsDown(human), Is.True, "Patient should be prone");
        });

        await server.WaitAssertion(() =>
        {
            stunSystem.TryStanding(human);
            stunSystem.ForceStandUp(human);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(standingState.IsDown(human), Is.True, "Should remain prone with no legs");
            Assert.That(entityManager.HasComponent<KnockedDownComponent>(human), Is.True,
                "Knockdown should persist after stand attempts");
            Assert.That(entityManager.HasComponent<LegsMissingComponent>(human), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            // Humanoid legs sit in the body's body_organs container (not a BodyPart slot); same pattern as
            // OrganInsertRequestEvent + torso for internals, or SharedContainerSystem.Insert for cyber limbs.
            var bodyComp = entityManager.GetComponent<BodyComponent>(human);
            Assert.That(bodyComp.Organs, Is.Not.Null);
            Assert.That(containerSystem.Insert(legLeft, bodyComp.Organs!), Is.True, "Re-insert leg should succeed");
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.HasComponent<LegsMissingComponent>(human), Is.False,
                "LegsMissingComponent removed when at least one leg returns");
            Assert.That(entityManager.HasComponent<KnockedDownComponent>(human), Is.False,
                "Knockdown cleared when legs restored");
            Assert.That(standingState.IsDown(human), Is.False, "Patient should stand after leg restored");
        });

        await pair.CleanReturnAsync();
    }
}
