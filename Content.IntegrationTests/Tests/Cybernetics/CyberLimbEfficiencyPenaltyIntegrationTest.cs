using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(CyberLimbStatsSystem))]
public sealed class CyberLimbEfficiencyPenaltyIntegrationTest
{
    private static EntityUid GetArmLeft(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("ArmLeft") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static void ReplaceArmWithCyberArm(IEntityManager entityManager, BodySystem bodySystem,
        SharedContainerSystem containerSystem, EntityUid body, EntityCoordinates coords)
    {
        var arm = GetArmLeft(entityManager, body);
        var removeEv = new OrganRemoveRequestEvent(arm) { Destination = coords };
        entityManager.EventBus.RaiseLocalEvent(arm, ref removeEv);
        Assert.That(removeEv.Success, Is.True, "Remove arm should succeed");

        var cyberArm = entityManager.SpawnEntity("OrganCyberArmLeft", coords);
        var bodyComp = entityManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have Organs container");
        Assert.That(containerSystem.Insert(cyberArm, bodyComp.Organs!), Is.True, "Insert cyber arm should succeed");
    }

    [Test]
    public async Task EfficiencyPenalty_ReducesMovementSpeed_WhenDepleted()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var movementSpeedSystem = entityManager.System<MovementSpeedModifierSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(patient).Coordinates;

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            Assert.That(entityManager.HasComponent<CyberLimbStatsComponent>(patient), Is.True,
                "Patient should have CyberLimbStatsComponent");

            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            stats.ServiceTimeRemaining = TimeSpan.Zero;
            stats.Efficiency = 0.5f;
            entityManager.Dirty(patient, stats);

            movementSpeedSystem.RefreshMovementSpeedModifiers(patient);

            Assert.That(entityManager.HasComponent<MovementSpeedModifierComponent>(patient), Is.True,
                "EnsureComp should have added MovementSpeedModifierComponent");
            var moveComp = entityManager.GetComponent<MovementSpeedModifierComponent>(patient);
            Assert.That(moveComp.WalkSpeedModifier, Is.EqualTo(0.5f),
                "WalkSpeedModifier should be 0.5 when efficiency is depleted");
            Assert.That(moveComp.SprintSpeedModifier, Is.EqualTo(0.5f),
                "SprintSpeedModifier should be 0.5 when efficiency is depleted");

            stats = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            stats.Efficiency = 1f;
            entityManager.Dirty(patient, stats);
            movementSpeedSystem.RefreshMovementSpeedModifiers(patient);

            moveComp = entityManager.GetComponent<MovementSpeedModifierComponent>(patient);
            Assert.That(moveComp.WalkSpeedModifier, Is.EqualTo(1.0f),
                "WalkSpeedModifier should be 1.0 when efficiency is restored");
            Assert.That(moveComp.SprintSpeedModifier, Is.EqualTo(1.0f),
                "SprintSpeedModifier should be 1.0 when efficiency is restored");
        });

        await pair.CleanReturnAsync();
    }
}
