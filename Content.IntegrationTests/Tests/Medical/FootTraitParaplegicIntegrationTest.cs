using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Traits.Assorted;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(FootTraitParaplegicComponent))]
public sealed class FootTraitParaplegicIntegrationTest
{
    private static EntityUid GetLegLeft(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("LegLeft") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static EntityUid GetFootLeft(IEntityManager entityManager, BodySystem bodySystem, EntityUid body)
    {
        return bodySystem.GetAllOrgans(body).First(o =>
            entityManager.TryGetComponent(o, out OrganComponent comp) && comp.Category?.Id == "FootLeft");
    }

    [Test]
    public async Task TraitParaplegicFeet_RemoveOneFoot_ImplantHealthy_Restamp()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var limbEffects = entityManager.System<LimbDetachmentEffectsSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);

            entityManager.AddComponent<PermanentParaplegiaComponent>(human);
            bodySystem.ApplyTraitParaplegiaToImplantedFeet(human);
            limbEffects.RefreshFootStateForBody(human);

            Assert.That(entityManager.HasComponent<FeetMissingComponent>(human), Is.True,
                "Both feet trait-paraplegic should force FeetMissing");

            var leftFoot = GetFootLeft(entityManager, bodySystem, human);
            var removeEv = new OrganRemoveRequestEvent(leftFoot)
            {
                Destination = entityManager.GetComponent<TransformComponent>(human).Coordinates
            };
            entityManager.EventBus.RaiseLocalEvent(leftFoot, ref removeEv);
            Assert.That(removeEv.Success, Is.True);

            Assert.That(entityManager.HasComponent<FeetMissingComponent>(human), Is.True,
                "Should stay FeetMissing with only trait-paraplegic feet");

            var legLeft = GetLegLeft(entityManager, human);
            var freshCoords = entityManager.GetComponent<TransformComponent>(human).Coordinates;
            var freshFoot = entityManager.SpawnEntity("OrganHumanFootLeft", freshCoords);
            Assert.That(entityManager.HasComponent<FootTraitParaplegicComponent>(freshFoot), Is.False);

            var insertEv = new OrganInsertRequestEvent(legLeft, freshFoot);
            entityManager.EventBus.RaiseLocalEvent(legLeft, ref insertEv);
            Assert.That(insertEv.Success, Is.True);

            Assert.That(entityManager.HasComponent<FeetMissingComponent>(human), Is.False,
                "One healthy foot should clear FeetMissing");
            Assert.That(entityManager.HasComponent<MissingLimbMovementModifierComponent>(human), Is.True,
                "Single-foot slowdown should apply");

            bodySystem.ApplyTraitParaplegiaToImplantedFeet(human);
            limbEffects.RefreshFootStateForBody(human);

            Assert.That(entityManager.HasComponent<FeetMissingComponent>(human), Is.True,
                "Re-stamping trait paraplegia on all feet should restore FeetMissing");
        });

        await pair.CleanReturnAsync();
    }
}
