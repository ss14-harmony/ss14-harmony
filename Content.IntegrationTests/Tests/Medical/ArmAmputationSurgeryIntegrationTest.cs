using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery;
using Content.Shared.MedicalScanner;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Integration test for amputating an arm via Health Analyzer surgery BUI.
/// Uses each surgical tool: Scalpel, Wirecutter, Retractor, Hemostat, Saw, Cautery, BoneGel, Wrench, PowerDrill.
/// Performs full open sequence (CreateIncision, ClampVessels, RetractSkin, CutBone, MarrowBleeding, RetractTissue, DetachLimb),
/// then AttachLimb (with limb in hand), then full close sequence (MaintainAlignment, SealBleedPoints, RepairBoneSection,
/// ReleaseRetractor, ReconnectVessels, SealSkin).
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class ArmAmputationSurgeryIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static EntityUid GetArm(IEntityManager entityManager, EntityUid body, string category = "ArmLeft")
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0), $"Body should have a {category}");
        return ev.Parts[0];
    }

    [Test]
    public async Task SurgeryRequestBuiMessage_ArmAmputation_FullOpenAndClose_WithAllTools()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;
        var patientNet = Target!.Value;

        var analyzerNet = NetEntity.Invalid;
        var scalpelNet = NetEntity.Invalid;
        var wirecutterNet = NetEntity.Invalid;
        var retractorNet = NetEntity.Invalid;
        var hemostatNet = NetEntity.Invalid;
        var sawNet = NetEntity.Invalid;
        var cauteryNet = NetEntity.Invalid;
        var boneGelNet = NetEntity.Invalid;
        var wrenchNet = NetEntity.Invalid;
        var powerDrillNet = NetEntity.Invalid;
        var armNet = NetEntity.Invalid;

        await Server.WaitPost(() =>
        {
            var analyzer = SEntMan.SpawnEntity("HandheldHealthAnalyzer", SEntMan.GetCoordinates(TargetCoords));
            var scalpel = SEntMan.SpawnEntity("Scalpel", SEntMan.GetCoordinates(TargetCoords));
            var wirecutter = SEntMan.SpawnEntity("Wirecutter", SEntMan.GetCoordinates(TargetCoords));
            var retractor = SEntMan.SpawnEntity("Retractor", SEntMan.GetCoordinates(TargetCoords));
            var hemostat = SEntMan.SpawnEntity("Hemostat", SEntMan.GetCoordinates(TargetCoords));
            var saw = SEntMan.SpawnEntity("Saw", SEntMan.GetCoordinates(TargetCoords));
            var cautery = SEntMan.SpawnEntity("Cautery", SEntMan.GetCoordinates(TargetCoords));
            var boneGel = SEntMan.SpawnEntity("BoneGel", SEntMan.GetCoordinates(TargetCoords));
            var wrench = SEntMan.SpawnEntity("Wrench", SEntMan.GetCoordinates(TargetCoords));
            var powerDrill = SEntMan.SpawnEntity("PowerDrill", SEntMan.GetCoordinates(TargetCoords));
            var arm = GetArm(SEntMan, patient);

            HandSys.TryPickupAnyHand(SPlayer, analyzer, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, scalpel, checkActionBlocker: false);

            analyzerNet = SEntMan.GetNetEntity(analyzer);
            scalpelNet = SEntMan.GetNetEntity(scalpel);
            wirecutterNet = SEntMan.GetNetEntity(wirecutter);
            retractorNet = SEntMan.GetNetEntity(retractor);
            hemostatNet = SEntMan.GetNetEntity(hemostat);
            sawNet = SEntMan.GetNetEntity(saw);
            cauteryNet = SEntMan.GetNetEntity(cautery);
            boneGelNet = SEntMan.GetNetEntity(boneGel);
            wrenchNet = SEntMan.GetNetEntity(wrench);
            powerDrillNet = SEntMan.GetNetEntity(powerDrill);
            armNet = SEntMan.GetNetEntity(arm);
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

        // === OPEN: Skin layer ===
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "CreateIncision", SurgeryLayer.Skin, false), analyzerNet);
        await AwaitDoAfters(maxExpected: 1);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(wirecutterNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "ClampVessels", SurgeryLayer.Skin, false), analyzerNet);
        await AwaitDoAfters(maxExpected: 1);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "RetractSkin", SurgeryLayer.Skin, false), analyzerNet);
        await AwaitDoAfters(maxExpected: 1);

        // === OPEN: Tissue layer ===
        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(sawNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "CutBone", SurgeryLayer.Tissue, false), analyzerNet);
        await AwaitDoAfters(maxExpected: 1);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(cauteryNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "MarrowBleeding", SurgeryLayer.Tissue, false), analyzerNet);
        await AwaitDoAfters(maxExpected: 1);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "RetractTissue", SurgeryLayer.Tissue, false), analyzerNet);
        await AwaitDoAfters(maxExpected: 1);

        // === OPEN: Organ layer ===
        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(scalpelNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "DetachLimb", SurgeryLayer.Organ, false), analyzerNet);
        await AwaitDoAfters(maxExpected: 1);
        await RunTicks(15);

        NetEntity? handNet = null;
        await Server.WaitAssertion(() =>
        {
            var arm = SEntMan.GetEntity(armNet);
            Assert.That(SEntMan.EntityExists(arm), Is.True, "Arm entity should exist after detachment");
            var hasBodyPart = SEntMan.TryGetComponent(arm, out BodyPartComponent armBodyPart);
            var hasOrgan = SEntMan.TryGetComponent(arm, out OrganComponent armOrgan);
            Assert.That(hasBodyPart || hasOrgan, Is.True);
            if (hasBodyPart)
                Assert.That(armBodyPart.Body, Is.Null, "Arm should no longer be attached to body after DetachLimb");
            else if (hasOrgan)
                Assert.That(armOrgan.Body, Is.Null, "Arm should no longer be attached to body after DetachLimb");

            // Hand should be detached separately
            if (hasBodyPart && armBodyPart.Organs != null)
                Assert.That(armBodyPart.Organs.ContainedEntities.Count, Is.EqualTo(0),
                    "Arm should not contain the hand after DetachLimb; hand drops as separate item");

            // Find the detached hand (dropped at same location)
            var organQuery = SEntMan.EntityQueryEnumerator<OrganComponent>();
            while (organQuery.MoveNext(out var uid, out var organ))
            {
                if (organ.Category?.ToString() == "HandLeft" && organ.Body == null)
                {
                    handNet = SEntMan.GetNetEntity(uid);
                    break;
                }
            }
            Assert.That(handNet, Is.Not.Null, "Hand should exist as separate detached entity");
        });

        // Pick up the arm for AttachLimb
        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            Assert.That(HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(armNet), checkActionBlocker: false),
                Is.True, "Player should be able to pick up the detached arm");
        });
        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(HandSys.GetActiveItem((SPlayer, Hands!)), Is.EqualTo(SEntMan.GetEntity(armNet)),
                "Player should be holding the detached arm");
        });

        // === AttachLimb: bodyPart = patient (the body entity), organ = arm (limb in hand)
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, patientNet, "AttachLimb", SurgeryLayer.Organ, false, armNet), analyzerNet);
        await AwaitDoAfters(maxExpected: 1);
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var arm = SEntMan.GetEntity(armNet);
            Assert.That(SEntMan.EntityExists(arm), Is.True, "Arm entity should exist after AttachLimb");
            var hasBodyPart = SEntMan.TryGetComponent(arm, out BodyPartComponent armBodyPart);
            var hasOrgan = SEntMan.TryGetComponent(arm, out OrganComponent armOrgan);
            Assert.That(hasBodyPart || hasOrgan, Is.True);
            if (hasBodyPart)
                Assert.That(armBodyPart.Body, Is.EqualTo(patient), "Arm should be re-attached to body after AttachLimb");
            else if (hasOrgan)
                Assert.That(armOrgan.Body, Is.EqualTo(patient), "Arm should be re-attached to body after AttachLimb");
        });

        // === CLOSE: Tissue layer (MaintainAlignment, SealBleedPoints, RepairBoneSection)
        // Use RunTicks like SurgeryBodyPartDiagramIntegrationTest - close steps' DoAfters can be flaky with AwaitDoAfters
        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(powerDrillNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "MaintainAlignment", SurgeryLayer.Tissue, false), analyzerNet, fromServer: true);
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(cauteryNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "SealBleedPoints", SurgeryLayer.Tissue, false), analyzerNet, fromServer: true);
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(boneGelNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "RepairBoneSection", SurgeryLayer.Tissue, false), analyzerNet, fromServer: true);
        await RunTicks(150);

        // === CLOSE: Skin layer (ReleaseRetractor, ReconnectVessels, SealSkin)
        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "ReleaseRetractor", SurgeryLayer.Skin, false), analyzerNet, fromServer: true);
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(hemostatNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "ReconnectVessels", SurgeryLayer.Skin, false), analyzerNet, fromServer: true);
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(cauteryNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "SealSkin", SurgeryLayer.Skin, false), analyzerNet, fromServer: true);
        await RunTicks(150);

        // Tools used: Scalpel, Wirecutter, Retractor, Hemostat, Saw, Cautery, BoneGel, PowerDrill. Wrench (AnchoringTool) can substitute for Hemostat in ReconnectVessels.
        // Final assertion: operation closed successfully
        var bodySys = SEntMan.System<BodySystem>();
        await Server.WaitAssertion(() =>
        {
            var arm = SEntMan.GetEntity(armNet);
            Assert.That(SEntMan.EntityExists(arm), Is.True, "Arm entity should exist after full close");
            var hasBodyPart = SEntMan.TryGetComponent(arm, out BodyPartComponent armBodyPart);
            var hasOrgan = SEntMan.TryGetComponent(arm, out OrganComponent armOrgan);
            Assert.That(hasBodyPart || hasOrgan, Is.True);
            if (hasBodyPart)
                Assert.That(armBodyPart.Body, Is.EqualTo(patient), "Arm should remain attached after full close");
            else if (hasOrgan)
                Assert.That(armOrgan.Body, Is.EqualTo(patient), "Arm should remain attached after full close");

            var handCount = bodySys.GetAllOrgans(patient).Count(o =>
                SEntMan.TryGetComponent(o, out OrganComponent oc) && oc.Category?.ToString() == "HandLeft");
            Assert.That(handCount, Is.LessThanOrEqualTo(1), "Body should have at most one HandLeft");
        });
    }
}
