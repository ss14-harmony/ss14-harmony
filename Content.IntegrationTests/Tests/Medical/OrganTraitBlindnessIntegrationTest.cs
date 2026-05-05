#nullable enable
using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Traits.Assorted;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(OrganTraitBlindnessComponent))]
public sealed class OrganTraitBlindnessIntegrationTest
{
    private static EntityUid GetHead(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Head") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static EntityUid GetEyes(IEntityManager entityManager, BodySystem bodySystem, EntityUid body)
    {
        return bodySystem.GetAllOrgans(body).First(o =>
            entityManager.TryGetComponent(o, out OrganComponent? comp) && comp.Category?.Id == "Eyes");
    }

    private static int CountEyeOrgans(IEntityManager entityManager, BodySystem bodySystem, EntityUid body)
    {
        return bodySystem.GetAllOrgans(body).Count(o =>
            entityManager.TryGetComponent(o, out OrganComponent? comp) && comp.Category?.Id == "Eyes");
    }

    [Test]
    public async Task TraitBlindEyes_ExplantEyeless_ImplantHealthySees_ImplantTraitBlindAgain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid human = default;
        EntityUid head = default;

        await server.WaitAssertion(() =>
        {
            human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            head = GetHead(entityManager, human);

            entityManager.AddComponent<PermanentBlindnessComponent>(human);
            Assert.That(entityManager.TryGetComponent(human, out BlindableComponent? blindable), Is.True);

            bodySystem.ApplyOrganTraitBlindnessToImplantedEyes(human, 0);
            bodySystem.RecalculateBlindnessFromOrgans(human);
            Assert.That(blindable!.MinDamage, Is.EqualTo((int)BlurryVisionComponent.MaxMagnitude),
                "Trait blind eyes should raise Blindable.MinDamage floor");
        });

        await server.WaitAssertion(() =>
        {
            var traitEyes = GetEyes(entityManager, bodySystem, human);
            var removeEv = new OrganRemoveRequestEvent(traitEyes);
            entityManager.EventBus.RaiseLocalEvent(traitEyes, ref removeEv);
            Assert.That(removeEv.Success, Is.True);

            // Removal places the organ in another container (e.g. the organ/item graph); delete it so queries and implants only see freshly spawned eyes.
            entityManager.DeleteEntity(traitEyes);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountEyeOrgans(entityManager, bodySystem, human), Is.Zero,
                "Explant should leave no implanted eye organs");

            Assert.That(entityManager.TryGetComponent(human, out BlindableComponent? blindable), Is.True);
            Assert.That(blindable!.IsBlind, Is.True, "Should stay blind without eyes");

            var freshCoords = entityManager.GetComponent<TransformComponent>(human).Coordinates;
            var freshEyes = entityManager.SpawnEntity("OrganHumanEyes", freshCoords);
            entityManager.RemoveComponent<OrganTraitBlindnessComponent>(freshEyes);
            Assert.That(entityManager.HasComponent<OrganTraitBlindnessComponent>(freshEyes), Is.False);

            var insertEv = new OrganInsertRequestEvent(head, freshEyes);
            entityManager.EventBus.RaiseLocalEvent(head, ref insertEv);
            Assert.That(insertEv.Success, Is.True);

            Assert.That(CountEyeOrgans(entityManager, bodySystem, human), Is.EqualTo(1));
            Assert.That(entityManager.HasComponent<OrganTraitBlindnessComponent>(freshEyes), Is.False,
                "Healthy implant should not carry organ trait blindness");

            Assert.That(blindable.MinDamage, Is.Zero, "Healthy implanted eyes should clear trait blindness floor");

            bodySystem.ApplyOrganTraitBlindnessToImplantedEyes(human, 0);
            bodySystem.RecalculateBlindnessFromOrgans(human);
            Assert.That(blindable.MinDamage, Is.EqualTo((int)BlurryVisionComponent.MaxMagnitude),
                "Re-stamping trait blindness on implanted eyes should restore blindness floor");
        });

        await pair.CleanReturnAsync();
    }
}
