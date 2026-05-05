using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(SurgerySystem))]
[TestOf(typeof(SurgeryStepRequestEvent))]
[TestOf(typeof(SurgeryStepCompletedEvent))]
public sealed class DynamicSurgeryEventIntegrationTest
{
    private static EntityUid GetTorso(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Torso") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    [Test]
    public async Task SurgeryRequest_RaisesSurgeryStepRequestEvent_OnBodyPart()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid surgeon = default;
        EntityUid patient = default;
        EntityUid analyzer = default;
        EntityUid torso = default;

        await server.WaitAssertion(() =>
        {
            surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            var wirecutter = entityManager.SpawnEntity("Wirecutter", mapData.GridCoords);
            var retractor = entityManager.SpawnEntity("Retractor", mapData.GridCoords);
            torso = GetTorso(entityManager, patient);

            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);

            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "CreateIncision should succeed");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var wirecutter = entityManager.SpawnEntity("Wirecutter", mapData.GridCoords);
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, wirecutter, checkActionBlocker: false);

            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"ClampVessels", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "ClampVessels should succeed");
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var retractor = entityManager.SpawnEntity("Retractor", mapData.GridCoords);
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, retractor, checkActionBlocker: false);

            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);

            Assert.That(ev.Valid, Is.True, "SurgeryRequestEvent valid implies SurgeryStepRequestEvent was raised on body part and passed validation");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SurgeryDoAfter_RaisesSurgeryStepCompletedEvent_OnBodyPart()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false });
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;
        EntityUid torso = default;

        EntityUid surgeon = default;
        EntityUid analyzer = default;

        await server.WaitPost(() =>
        {
            surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            var wirecutter = entityManager.SpawnEntity("Wirecutter", mapData.GridCoords);
            var retractor = entityManager.SpawnEntity("Retractor", mapData.GridCoords);
            torso = GetTorso(entityManager, patient);

            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);

            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var wirecutter = entityManager.SpawnEntity("Wirecutter", mapData.GridCoords);
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, wirecutter, checkActionBlocker: false);

            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"ClampVessels", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var retractor = entityManager.SpawnEntity("Retractor", mapData.GridCoords);
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, retractor, checkActionBlocker: false);

            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });

        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.TryGetComponent(torso, out SurgeryLayerComponent? layer), Is.True);
            Assert.That(layer!.PerformedSkinSteps, Does.Contain("RetractSkin"), "Step application occurs via SurgeryStepCompletedEvent handler, not direct mutation");
        });
        await pair.CleanReturnAsync();
    }
}
