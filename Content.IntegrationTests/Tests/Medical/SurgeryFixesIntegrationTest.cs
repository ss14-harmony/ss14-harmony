// SPDX-FileCopyrightText: 2026 Fruitsalad <949631+Fruitsalad@users.noreply.github.com>
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Client.HealthAnalyzer.UI;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.MedicalScanner;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Integration tests for surgery bug fixes:
/// - CutBone rejects scalpel (requires SawingTool, no improvised CuttingTool)
/// - DetachLimb on leg detaches leg and foot as separate items
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class SurgeryFixesIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static EntityUid GetBodyPart(IEntityManager entityManager, EntityUid body, string category)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0), $"Body should have a {category}");
        return ev.Parts[0];
    }

    /// <summary>
    /// CutBone requires SawingTool. Scalpel has CuttingTool which was incorrectly allowed as improvised.
    /// With the fix, CutBone with only scalpel should be rejected with missing-tool.
    /// </summary>
    [Test]
    public async Task CutBone_WithScalpelOnly_Rejected()
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

        await server.WaitPost(() =>
        {
            surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            torso = GetBodyPart(entityManager, patient, "Torso");

            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);
        });

        await pair.RunTicksSync(5);

        // Open skin layer first (CutBone requires CreateIncision, ClampVessels, RetractSkin)
        await server.WaitPost(() =>
        {
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "CreateIncision should succeed");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, scalpel, checkActionBlocker: false);
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

        // Swap back to scalpel - CutBone requires SawingTool, scalpel has CuttingTool
        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);
        });
        await pair.RunTicksSync(5);

        // Try CutBone with only scalpel - should be rejected (CutBone requires SawingTool)
        await server.WaitAssertion(() =>
        {
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CutBone", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);

            Assert.That(ev.Valid, Is.False, "CutBone should be rejected when only holding scalpel");
            Assert.That(ev.RejectReason, Is.EqualTo("missing-tool"), "Reject reason should be missing-tool");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// CutBone accepts any item with blunt damage (e.g. crowbar) as improvised tool with blunt-scaled speed.
    /// </summary>
    [Test]
    public async Task CutBone_WithCrowbar_Accepted()
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
        EntityUid crowbar = default;
        EntityUid scalpel = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            crowbar = entityManager.SpawnEntity("Crowbar", mapData.GridCoords);
            scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            torso = GetBodyPart(entityManager, patient, "Torso");

            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);
        });

        await pair.RunTicksSync(5);

        // Open skin layer first (CutBone requires CreateIncision, ClampVessels, RetractSkin)
        await server.WaitPost(() =>
        {
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "CreateIncision should succeed");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, scalpel, checkActionBlocker: false);
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

        // Swap retractor for crowbar
        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, crowbar, checkActionBlocker: false);
        });
        await pair.RunTicksSync(5);

        // CutBone with crowbar (BluntTool) should be accepted as improvised
        await server.WaitAssertion(() =>
        {
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CutBone", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);

            Assert.That(ev.Valid, Is.True, "CutBone should succeed when holding crowbar (deals blunt damage)");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// When DetachLimb is performed on a leg, both the leg and foot should be detached as separate items.
    /// Verifies the leg entity is detached and the foot is also detached (not inside the leg).
    /// </summary>
    [Test]
    public async Task DetachLimb_OnLeg_DetachesLegAndFootSeparately()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;
        var patientNet = Target!.Value;

        var analyzerNet = NetEntity.Invalid;
        var scalpelNet = NetEntity.Invalid;
        var wirecutterNet = NetEntity.Invalid;
        var retractorNet = NetEntity.Invalid;
        var sawNet = NetEntity.Invalid;
        var cauteryNet = NetEntity.Invalid;
        var legNet = NetEntity.Invalid;

        await Server.WaitPost(() =>
        {
            var analyzer = SEntMan.SpawnEntity("HandheldHealthAnalyzer", SEntMan.GetCoordinates(TargetCoords));
            var scalpel = SEntMan.SpawnEntity("Scalpel", SEntMan.GetCoordinates(TargetCoords));
            var wirecutter = SEntMan.SpawnEntity("Wirecutter", SEntMan.GetCoordinates(TargetCoords));
            var retractor = SEntMan.SpawnEntity("Retractor", SEntMan.GetCoordinates(TargetCoords));
            var saw = SEntMan.SpawnEntity("Saw", SEntMan.GetCoordinates(TargetCoords));
            var cautery = SEntMan.SpawnEntity("Cautery", SEntMan.GetCoordinates(TargetCoords));
            var leg = GetBodyPart(SEntMan, patient, "LegLeft");

            HandSys.TryPickupAnyHand(SPlayer, analyzer, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, scalpel, checkActionBlocker: false);

            analyzerNet = SEntMan.GetNetEntity(analyzer);
            scalpelNet = SEntMan.GetNetEntity(scalpel);
            wirecutterNet = SEntMan.GetNetEntity(wirecutter);
            retractorNet = SEntMan.GetNetEntity(retractor);
            sawNet = SEntMan.GetNetEntity(saw);
            cauteryNet = SEntMan.GetNetEntity(cautery);
            legNet = SEntMan.GetNetEntity(leg);
        });

        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            var analyzerUid = SEntMan.GetEntity(analyzerNet);
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == analyzerUid)
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    break;
                }
            }
        });

        await RunTicks(1);
        await Interact(awaitDoAfters: true);
        Assert.That(IsUiOpen(HealthAnalyzerUiKey.Key), Is.True, "Health Analyzer BUI should open after scan");

        await Server.WaitPost(() =>
        {
            var scalpelUid = SEntMan.GetEntity(scalpelNet);
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == scalpelUid)
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    break;
                }
            }
        });

        await RunTicks(1);

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "CreateIncision", SurgeryLayer.Skin, false), analyzerNet, fromServer: true);
        await AwaitDoAfters(maxExpected: 1, minExpected: 1);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(wirecutterNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "ClampVessels", SurgeryLayer.Skin, false), analyzerNet, fromServer: true);
        await AwaitDoAfters(maxExpected: 1, minExpected: 1);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "RetractSkin", SurgeryLayer.Skin, false), analyzerNet, fromServer: true);
        await AwaitDoAfters(maxExpected: 1, minExpected: 1);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(sawNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "CutBone", SurgeryLayer.Tissue, false), analyzerNet, fromServer: true);
        await AwaitDoAfters(maxExpected: 1, minExpected: 1);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(cauteryNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "MarrowBleeding", SurgeryLayer.Tissue, false), analyzerNet, fromServer: true);
        await AwaitDoAfters(maxExpected: 1, minExpected: 1);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "RetractTissue", SurgeryLayer.Tissue, false), analyzerNet, fromServer: true);
        await AwaitDoAfters(maxExpected: 1, minExpected: 1);

        // DetachLimb requires CuttingTool (scalpel); swap from retractor
        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(scalpelNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "DetachLimb", SurgeryLayer.Organ, false), analyzerNet, fromServer: true);
        await AwaitDoAfters(maxExpected: 1, minExpected: 1);
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var leg = SEntMan.GetEntity(legNet);
            Assert.That(SEntMan.EntityExists(leg), Is.True, "Leg entity should exist after detachment");
            Assert.That(SEntMan.TryGetComponent(leg, out BodyPartComponent? bodyPart), Is.True);
            Assert.That(bodyPart!.Body, Is.Null, "Leg should no longer be attached to body after DetachLimb");

            // Leg and foot should be detached as separate items - leg should not contain the foot
            Assert.That(bodyPart.Organs?.ContainedEntities.Count ?? 0, Is.EqualTo(0),
                "Leg should not contain the foot after DetachLimb; foot drops as separate item");

            // Verify the foot exists as a separate detached entity
            var footFound = false;
            var footQuery = SEntMan.EntityQueryEnumerator<OrganComponent>();
            while (footQuery.MoveNext(out _, out var organ))
            {
                if (organ.Category?.ToString() == "FootLeft" && organ.Body == null)
                {
                    footFound = true;
                    break;
                }
            }
            Assert.That(footFound, Is.True, "Foot should exist as separate detached entity");
        });
    }

    /// <summary>
    /// SurgeryBodyPartDiagram region order: arms must come before hands, legs before feet,
    /// so clicking on an arm/leg selects the whole limb for DetachLimb, not just the hand/foot.
    /// </summary>
    [Test]
    public void SurgeryBodyPartDiagram_RegionOrder_ArmsBeforeHandsLegsBeforeFeet()
    {
        var order = SurgeryBodyPartDiagramControl.RegionCategoryOrder.ToList();

        var armRightIdx = order.IndexOf("ArmRight");
        var handRightIdx = order.IndexOf("HandRight");
        Assert.That(armRightIdx, Is.GreaterThanOrEqualTo(0), "ArmRight must exist in region order");
        Assert.That(armRightIdx, Is.LessThan(handRightIdx), "ArmRight must come before HandRight in region order");

        var armLeftIdx = order.IndexOf("ArmLeft");
        var handLeftIdx = order.IndexOf("HandLeft");
        Assert.That(armLeftIdx, Is.LessThan(handLeftIdx), "ArmLeft must come before HandLeft in region order");

        var legRightIdx = order.IndexOf("LegRight");
        var footRightIdx = order.IndexOf("FootRight");
        Assert.That(legRightIdx, Is.LessThan(footRightIdx), "LegRight must come before FootRight in region order");

        var legLeftIdx = order.IndexOf("LegLeft");
        var footLeftIdx = order.IndexOf("FootLeft");
        Assert.That(legLeftIdx, Is.LessThan(footLeftIdx), "LegLeft must come before FootLeft in region order");
    }
}
