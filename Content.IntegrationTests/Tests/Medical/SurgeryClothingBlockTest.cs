using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Surgery is blocked on Torso when outerClothing is worn and on Head when head slot is worn.
/// </summary>
[TestFixture]
[TestOf(typeof(SurgerySystem))]
public sealed class SurgeryClothingBlockTest
{
    private static EntityUid GetBodyPart(IEntityManager entityManager, EntityUid body, string category)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0), $"Body should have {category}");
        return ev.Parts[0];
    }

    [Test]
    public async Task Torso_CreateIncision_AllowedWithoutOuterwear_BlockedWithOuterwear()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var inventorySystem = entityManager.System<InventorySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var coords = mapData.GridCoords;
            var patient = entityManager.SpawnEntity("MobHuman", coords);
            var surgeon = entityManager.SpawnEntity("MobHuman", coords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", coords);
            var scalpel = entityManager.SpawnEntity("Scalpel", coords);
            var torso = GetBodyPart(entityManager, patient, "Torso");

            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);

            var evOk = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref evOk);
            Assert.That(evOk.Valid, Is.True, $"CreateIncision without outerwear should succeed. Reject: {evOk.RejectReason}");

            var coat = entityManager.SpawnEntity("ClothingOuterCoatLab", coords);
            Assert.That(inventorySystem.TryEquip(surgeon, patient, coat, "outerClothing", silent: true, force: true),
                Is.True, "Equip coat on patient");

            var evBlocked = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref evBlocked);
            Assert.That(evBlocked.Valid, Is.False, "CreateIncision with outerwear should fail");
            Assert.That(evBlocked.RejectReason, Is.EqualTo("clothing-in-the-way"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Head_CreateIncision_AllowedWithoutHeadwear_BlockedWithHeadwear()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var inventorySystem = entityManager.System<InventorySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var coords = mapData.GridCoords;
            var patient = entityManager.SpawnEntity("MobHuman", coords);
            var surgeon = entityManager.SpawnEntity("MobHuman", coords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", coords);
            var scalpel = entityManager.SpawnEntity("Scalpel", coords);
            var head = GetBodyPart(entityManager, patient, "Head");

            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);

            var evOk = new SurgeryRequestEvent(analyzer, surgeon, patient, head, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref evOk);
            Assert.That(evOk.Valid, Is.True, $"CreateIncision without headwear should succeed. Reject: {evOk.RejectReason}");

            var hat = entityManager.SpawnEntity("ClothingHeadHatBeret", coords);
            Assert.That(inventorySystem.TryEquip(surgeon, patient, hat, "head", silent: true, force: true),
                Is.True, "Equip hat on patient");

            var evBlocked = new SurgeryRequestEvent(analyzer, surgeon, patient, head, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref evBlocked);
            Assert.That(evBlocked.Valid, Is.False, "CreateIncision with head slot filled should fail");
            Assert.That(evBlocked.RejectReason, Is.EqualTo("clothing-in-the-way"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ArmLeft_CreateIncision_SucceedsWhenPatientWearsOuterwear()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var inventorySystem = entityManager.System<InventorySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var coords = mapData.GridCoords;
            var patient = entityManager.SpawnEntity("MobHuman", coords);
            var surgeon = entityManager.SpawnEntity("MobHuman", coords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", coords);
            var scalpel = entityManager.SpawnEntity("Scalpel", coords);
            var arm = GetBodyPart(entityManager, patient, "ArmLeft");

            var coat = entityManager.SpawnEntity("ClothingOuterCoatLab", coords);
            Assert.That(inventorySystem.TryEquip(surgeon, patient, coat, "outerClothing", silent: true, force: true),
                Is.True, "Equip coat on patient");

            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);

            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, arm, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"Limb surgery should not be blocked by outerwear. Reject: {ev.RejectReason}");
        });

        await pair.CleanReturnAsync();
    }
}
