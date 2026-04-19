using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Integrity.Events;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(IntegrityUsageSystem))]
public sealed class IntegrityUsageIntegrationTest
{
    private static EntityUid GetTorso(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Torso") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static EntityUid GetHeart(IEntityManager entityManager, BodySystem bodySystem, EntityUid body)
    {
        return bodySystem.GetAllOrgans(body).First(o =>
            entityManager.TryGetComponent(o, out OrganComponent? comp) && comp.Category?.Id == "Heart");
    }

    [Test]
    public async Task IntegrityUsage_StoresAndUpdates_OnOrganInsertAndRemove()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso = GetTorso(entityManager, human);
            var heart = GetHeart(entityManager, bodySystem, human);

            var removeEv = new OrganRemoveRequestEvent(heart);
            entityManager.EventBus.RaiseLocalEvent(heart, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove should succeed");

            var biosyntheticHeart = entityManager.SpawnEntity("OrganBiosyntheticHeart", entityManager.GetComponent<TransformComponent>(human).Coordinates);
            var insertEv = new OrganInsertRequestEvent(torso, biosyntheticHeart);
            entityManager.EventBus.RaiseLocalEvent(torso, ref insertEv);
            Assert.That(insertEv.Success, Is.True, "Insert should succeed");

            Assert.That(entityManager.TryGetComponent(human, out IntegrityUsageComponent? usageComp), Is.True, "Body should have IntegrityUsageComponent after inserting cost-1 organ");
            Assert.That(usageComp!.Usage, Is.EqualTo(1), "Usage should be 1 after inserting biosynthetic heart");

            var removeBiosyntheticEv = new OrganRemoveRequestEvent(biosyntheticHeart);
            entityManager.EventBus.RaiseLocalEvent(biosyntheticHeart, ref removeBiosyntheticEv);
            Assert.That(removeBiosyntheticEv.Success, Is.True, "Remove biosynthetic heart should succeed");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InsertOrgan_OverCapacity_Succeeds()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false
        });
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        await pair.RunTicksSync(5);

        EntityUid surgeon = default;
        EntityUid patient = default;
        EntityUid analyzer = default;
        EntityUid scalpel = default;
        EntityUid saw = default;
        EntityUid torso = default;
        EntityUid biosyntheticHeart = default;

        await server.WaitPost(() =>
        {
            surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            saw = entityManager.SpawnEntity("Saw", mapData.GridCoords);
            torso = GetTorso(entityManager, patient);
            var naturalHeart = GetHeart(entityManager, entityManager.System<BodySystem>(), patient);
            var removeHeartEv = new OrganRemoveRequestEvent(naturalHeart);
            entityManager.EventBus.RaiseLocalEvent(naturalHeart, ref removeHeartEv);
            Assert.That(removeHeartEv.Success, Is.True, "Remove natural heart to free slot");
            biosyntheticHeart = entityManager.SpawnEntity("OrganBiosyntheticHeart", entityManager.GetComponent<TransformComponent>(patient).Coordinates);

            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);

            var penaltyEv = new IntegrityPenaltyAppliedEvent(patient, 6, "test", IntegrityPenaltyCategory.DirtyRoom);
            entityManager.EventBus.RaiseLocalEvent(patient, ref penaltyEv);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "CreateIncision should succeed");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, entityManager.SpawnEntity("Wirecutter", mapData.GridCoords), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"ClampVessels", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "ClampVessels should succeed");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, entityManager.SpawnEntity("Retractor", mapData.GridCoords), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "RetractSkin should succeed");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, saw, checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CutBone", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "CutBone should succeed");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, entityManager.SpawnEntity("Cautery", mapData.GridCoords), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"MarrowBleeding", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "MarrowBleeding should succeed");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, entityManager.SpawnEntity("Retractor", mapData.GridCoords), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"RetractTissue", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "RetractTissue should succeed");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, biosyntheticHeart, checkActionBlocker: false);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"InsertOrgan", SurgeryLayer.Organ, false, biosyntheticHeart);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);

            Assert.That(ev.Valid, Is.True, "InsertOrgan should succeed even when over capacity; bio-rejection applies instead");
        });

        await pair.CleanReturnAsync();
    }
}
