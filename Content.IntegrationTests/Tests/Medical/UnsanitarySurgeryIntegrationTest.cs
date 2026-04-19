using System.Collections.Generic;
using System.Linq;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Server.Medical.Integrity;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Integrity.Events;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(UnsanitarySurgeryCalculationSystem))]
public sealed class UnsanitarySurgeryIntegrationTest
{
    private static EntityUid GetTorso(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Torso") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    [Test]
    public async Task Surgery_OnSterileFloor_MinimalUnsanitaryPenalty()
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
        EntityUid torso = default;

        EntityUid wirecutter = default;
        EntityUid retractor = default;

        await server.WaitPost(() =>
        {
            surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            wirecutter = entityManager.SpawnEntity("Wirecutter", mapData.GridCoords);
            retractor = entityManager.SpawnEntity("Retractor", mapData.GridCoords);
            torso = GetTorso(entityManager, patient);

            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"CreateIncision should be valid. RejectReason: {ev.RejectReason}");
        });

        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, wirecutter, checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"ClampVessels", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"ClampVessels should be valid. RejectReason: {ev.RejectReason}");
        });

        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, retractor, checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"RetractSkin should be valid. RejectReason: {ev.RejectReason}");
        });

        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.TryGetComponent(torso, out SurgeryLayerComponent? layer), Is.True, "Should have SurgeryLayerComponent on torso");
            Assert.That(layer!.SkinRetracted, Is.True, "Skin should be retracted after DoAfter");
            var totalEv = new IntegrityPenaltyTotalRequestEvent(patient);
            entityManager.EventBus.RaiseLocalEvent(patient, ref totalEv);
            Assert.That(totalEv.Total, Is.GreaterThanOrEqualTo(1), "Should have at least step penalty (1)");
            if (entityManager.TryGetComponent(patient, out IntegritySurgeryComponent? surgeryComp))
            {
                var unsanitaryEntries = surgeryComp.Entries.Where(e => e.Category == IntegrityPenaltyCategory.UnsanitarySurgery).ToList();
                Assert.That(unsanitaryEntries.Sum(e => e.Amount), Is.LessThanOrEqualTo(1), "On sterile floor, UnsanitarySurgery penalty should be minimal");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Surgery_WithPuddle_AppliesUnsanitaryPenalty()
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
        var puddleSystem = entityManager.System<PuddleSystem>();
        var mapSystem = entityManager.System<SharedMapSystem>();
        var mapData = await pair.CreateTestMap();

        await pair.RunTicksSync(5);

        EntityUid surgeon = default;
        EntityUid patient = default;
        EntityUid analyzer = default;
        EntityUid scalpel = default;
        EntityUid torso = default;
        EntityUid wirecutter = default;
        EntityUid retractor = default;

        await server.WaitPost(() =>
        {
            var tile = mapData.Tile;
            var spawnCoords = mapSystem.GridTileToLocal(tile.GridUid, entityManager.GetComponent<MapGridComponent>(tile.GridUid), tile.GridIndices);
            surgeon = entityManager.SpawnEntity("MobHuman", spawnCoords);
            patient = entityManager.SpawnEntity("MobHuman", spawnCoords);
            analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", spawnCoords);
            scalpel = entityManager.SpawnEntity("Scalpel", spawnCoords);
            wirecutter = entityManager.SpawnEntity("Wirecutter", spawnCoords);
            retractor = entityManager.SpawnEntity("Retractor", spawnCoords);
            torso = GetTorso(entityManager, patient);

            // Spill blood directly - water is excluded from unsanitary penalty, so use blood to test detection
            var solution = new Solution("Blood", FixedPoint2.New(50));
            Assert.That(puddleSystem.TrySpillAt(spawnCoords, solution, out _), Is.True, "Should spill blood");

            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);
        });

        // Wait for spill to process and puddle to settle
        await pair.RunTicksSync(10);

        await server.WaitPost(() =>
        {
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"CreateIncision should be valid. RejectReason: {ev.RejectReason}");
        });

        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, wirecutter, checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"ClampVessels", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"ClampVessels should be valid. RejectReason: {ev.RejectReason}");
        });

        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, retractor, checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"RetractSkin should be valid. RejectReason: {ev.RejectReason}");
        });

        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.TryGetComponent(torso, out SurgeryLayerComponent? layer), Is.True, "Should have SurgeryLayerComponent on torso");
            Assert.That(layer!.SkinRetracted, Is.True, "Skin should be retracted after DoAfter");
            var totalEv = new IntegrityPenaltyTotalRequestEvent(patient);
            entityManager.EventBus.RaiseLocalEvent(patient, ref totalEv);
            Assert.That(totalEv.Total, Is.GreaterThanOrEqualTo(1), "Should have at least step penalty (1)");
            Assert.That(entityManager.TryGetComponent(patient, out IntegritySurgeryComponent? surgeryComp), Is.True, "Patient should have IntegritySurgeryComponent after surgery");
            var unsanitaryEntries = surgeryComp!.Entries.Where(e => e.Category == IntegrityPenaltyCategory.UnsanitarySurgery).ToList();
            Assert.That(unsanitaryEntries.Sum(e => e.Amount), Is.GreaterThan(0), "With puddle under patient, UnsanitarySurgery penalty should be applied");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnsanitaryPenalty_WithPuddleOnTile_DetectsPuddle()
    {
        // Direct test of puddle detection: spawn patient, spill puddle, request penalty, assert applied
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false });
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var puddleSystem = entityManager.System<PuddleSystem>();
        var mapSystem = entityManager.System<SharedMapSystem>();
        var mapData = await pair.CreateTestMap();

        await pair.RunTicksSync(5);

        EntityUid patient = default;
        await server.WaitPost(() =>
        {
            var tile = mapData.Tile;
            var spawnCoords = mapSystem.GridTileToLocal(tile.GridUid, entityManager.GetComponent<MapGridComponent>(tile.GridUid), tile.GridIndices);
            patient = entityManager.SpawnEntity("MobHuman", spawnCoords);

            // Spill blood - water is excluded from unsanitary penalty, so use blood to test detection
            var solution = new Solution("Blood", FixedPoint2.New(50));
            Assert.That(puddleSystem.TrySpillAt(spawnCoords, solution, out _), Is.True, "Should spill blood");
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var ev = new UnsanitarySurgeryPenaltyRequestEvent(patient, GetTorso(entityManager, patient), "TestStep", SurgeryLayer.Skin, false, null, null);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);

            Assert.That(entityManager.TryGetComponent(patient, out IntegritySurgeryComponent? surgeryComp), Is.True, "Patient should have IntegritySurgeryComponent after penalty request with puddle");
            var unsanitaryEntries = surgeryComp!.Entries.Where(e => e.Category == IntegrityPenaltyCategory.UnsanitarySurgery).ToList();
            Assert.That(unsanitaryEntries.Sum(e => e.Amount), Is.GreaterThan(0), "UnsanitarySurgery penalty should be applied when puddle on tile");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IntegrityPenalty_HierarchicalImproperTools_StoredAndAggregated()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false });
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var improvisedChild = new IntegrityPenaltyEntry("health-analyzer-integrity-improvised-tool", IntegrityPenaltyCategory.ImproperTools, 1, null);
            var stepEntry = new IntegrityPenaltyEntry("health-analyzer-surgery-step-retract-skin", IntegrityPenaltyCategory.ImproperTools, 1, new List<IntegrityPenaltyEntry> { improvisedChild });
            var children = new List<IntegrityPenaltyEntry> { stepEntry };
            var applyEv = new IntegrityPenaltyAppliedEvent(patient, 2, "Torso", IntegrityPenaltyCategory.ImproperTools, children);
            entityManager.EventBus.RaiseLocalEvent(patient, ref applyEv);

            Assert.That(entityManager.TryGetComponent(patient, out IntegritySurgeryComponent? comp), Is.True);
            var improperEntries = comp!.Entries.Where(e => e.Category == IntegrityPenaltyCategory.ImproperTools).ToList();
            Assert.That(improperEntries, Has.Count.EqualTo(1));
            Assert.That(improperEntries[0].Children, Is.Not.Null);
            Assert.That(improperEntries[0].Children!.Count, Is.EqualTo(1));
            Assert.That(improperEntries[0].Children![0].Children, Is.Not.Null);
            Assert.That(improperEntries[0].Children![0].Children!.Count, Is.EqualTo(1));

            var totalEv = new IntegrityPenaltyTotalRequestEvent(patient);
            entityManager.EventBus.RaiseLocalEvent(patient, ref totalEv);
            Assert.That(totalEv.Total, Is.EqualTo(2));
        });

        await pair.CleanReturnAsync();
    }
}
