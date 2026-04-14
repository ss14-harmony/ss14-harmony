using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.MedicalScanner;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Integration test that verifies surgery layer state (IsSkinOpen, IsTissueOpen, IsOrganLayerOpen)
/// is computed from config and not hardcoded. Uses InteractionTest with attached player as surgeon,
/// SendBui for surgery requests, and AwaitDoAfters for reliable DoAfter completion.
/// </summary>
[TestFixture]
[TestOf(typeof(SurgeryLayerSystem))]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class LayerStateSurgeryIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static EntityUid GetTorso(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Torso") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    [Test]
    public async Task LayerState_ComputedFromConfig_NotHardcoded()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;
        var patientNet = Target!.Value;

        var surgeryLayer = SEntMan.System<SurgeryLayerSystem>();
        var analyzerNet = NetEntity.Invalid;
        var scalpelNet = NetEntity.Invalid;
        var wirecutterNet = NetEntity.Invalid;
        var retractorNet = NetEntity.Invalid;
        var sawNet = NetEntity.Invalid;
        var cauteryNet = NetEntity.Invalid;
        var torsoNet = NetEntity.Invalid;

        await Server.WaitPost(() =>
        {
            var torso = GetTorso(SEntMan, patient);
            var layerComp = SEntMan.EnsureComponent<SurgeryLayerComponent>(torso);
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, torso);
            Assert.That(stepsConfig, Is.Not.Null);
            Assert.That(surgeryLayer.IsSkinOpen(layerComp, stepsConfig!), Is.False);
            Assert.That(surgeryLayer.IsTissueOpen(layerComp, stepsConfig!), Is.False);
            Assert.That(surgeryLayer.IsOrganLayerOpen(layerComp, stepsConfig!), Is.False);
        });

        await Server.WaitPost(() =>
        {
            var analyzer = SEntMan.SpawnEntity("HandheldHealthAnalyzer", SEntMan.GetCoordinates(TargetCoords));
            var scalpel = SEntMan.SpawnEntity("Scalpel", SEntMan.GetCoordinates(TargetCoords));
            var wirecutter = SEntMan.SpawnEntity("Wirecutter", SEntMan.GetCoordinates(TargetCoords));
            var retractor = SEntMan.SpawnEntity("Retractor", SEntMan.GetCoordinates(TargetCoords));
            var saw = SEntMan.SpawnEntity("Saw", SEntMan.GetCoordinates(TargetCoords));
            var cautery = SEntMan.SpawnEntity("Cautery", SEntMan.GetCoordinates(TargetCoords));
            var torso = GetTorso(SEntMan, patient);

            HandSys.TryPickupAnyHand(SPlayer, analyzer, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, scalpel, checkActionBlocker: false);

            analyzerNet = SEntMan.GetNetEntity(analyzer);
            scalpelNet = SEntMan.GetNetEntity(scalpel);
            wirecutterNet = SEntMan.GetNetEntity(wirecutter);
            retractorNet = SEntMan.GetNetEntity(retractor);
            sawNet = SEntMan.GetNetEntity(saw);
            cauteryNet = SEntMan.GetNetEntity(cautery);
            torsoNet = SEntMan.GetNetEntity(torso);
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

        // CreateIncision
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
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "CreateIncision", SurgeryLayer.Skin, false), analyzerNet, fromServer: true);
        await AwaitDoAfters(maxExpected: 1, minExpected: 1);

        // ClampVessels
        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(wirecutterNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "ClampVessels", SurgeryLayer.Skin, false), analyzerNet, fromServer: true);
        await AwaitDoAfters(maxExpected: 1, minExpected: 1);

        // RetractSkin
        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "RetractSkin", SurgeryLayer.Skin, false), analyzerNet, fromServer: true);
        await AwaitDoAfters(maxExpected: 1, minExpected: 1);

        await Server.WaitAssertion(() =>
        {
            var torso = SEntMan.GetEntity(torsoNet);
            var layerComp = SEntMan.GetComponent<SurgeryLayerComponent>(torso);
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, torso)!;
            Assert.That(surgeryLayer.IsSkinOpen(layerComp, stepsConfig), Is.True);
            Assert.That(surgeryLayer.IsTissueOpen(layerComp, stepsConfig), Is.False);
        });

        // CutBone
        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(sawNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "CutBone", SurgeryLayer.Tissue, false), analyzerNet, fromServer: true);
        await AwaitDoAfters(maxExpected: 1, minExpected: 1);

        // MarrowBleeding
        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(cauteryNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "MarrowBleeding", SurgeryLayer.Tissue, false), analyzerNet, fromServer: true);
        await AwaitDoAfters(maxExpected: 1, minExpected: 1);

        // RetractTissue
        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "RetractTissue", SurgeryLayer.Tissue, false), analyzerNet, fromServer: true);
        await AwaitDoAfters(maxExpected: 1, minExpected: 1);

        await Server.WaitAssertion(() =>
        {
            var torso = SEntMan.GetEntity(torsoNet);
            var layerComp = SEntMan.GetComponent<SurgeryLayerComponent>(torso);
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, torso)!;
            Assert.That(surgeryLayer.IsTissueOpen(layerComp, stepsConfig), Is.True);
            Assert.That(surgeryLayer.IsOrganLayerOpen(layerComp, stepsConfig), Is.True);
        });
    }
}
