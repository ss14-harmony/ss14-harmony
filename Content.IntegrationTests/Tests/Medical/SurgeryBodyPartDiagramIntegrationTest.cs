using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Integrity.Events;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.MedicalScanner;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Integration test for the Health Analyzer surgery BUI flow, including body part selection via the diagram.
/// Validates that SurgeryRequestBuiMessage flows correctly from client to server.
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class SurgeryBodyPartDiagramIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static EntityUid GetTorso(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Torso") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    [Test]
    public async Task SurgeryRequestBuiMessage_RetractSkin_CompletesAndAppliesPenalty()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;
        var patientNet = Target!.Value;

        var analyzerNet = NetEntity.Invalid;
        var scalpelNet = NetEntity.Invalid;
        var wirecutterNet = NetEntity.Invalid;
        var retractorNet = NetEntity.Invalid;
        var torsoNet = NetEntity.Invalid;

        await Server.WaitPost(() =>
        {
            var analyzer = SEntMan.SpawnEntity("HandheldHealthAnalyzer", SEntMan.GetCoordinates(TargetCoords));
            var scalpel = SEntMan.SpawnEntity("Scalpel", SEntMan.GetCoordinates(TargetCoords));
            var wirecutter = SEntMan.SpawnEntity("Wirecutter", SEntMan.GetCoordinates(TargetCoords));
            var retractor = SEntMan.SpawnEntity("Retractor", SEntMan.GetCoordinates(TargetCoords));
            var torso = GetTorso(SEntMan, patient);

            HandSys.TryPickupAnyHand(SPlayer, analyzer, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, scalpel, checkActionBlocker: false);

            analyzerNet = SEntMan.GetNetEntity(analyzer);
            scalpelNet = SEntMan.GetNetEntity(scalpel);
            wirecutterNet = SEntMan.GetNetEntity(wirecutter);
            retractorNet = SEntMan.GetNetEntity(retractor);
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
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(wirecutterNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "ClampVessels", SurgeryLayer.Skin, false), analyzerNet, fromServer: true);
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "RetractSkin", SurgeryLayer.Skin, false), analyzerNet, fromServer: true);
        await RunTicks(150);

        await Server.WaitAssertion(() =>
        {
            var torsoEnt = SEntMan.GetEntity(torsoNet);
            Assert.That(SEntMan.TryGetComponent(torsoEnt, out SurgeryLayerComponent? layer), Is.True);
            Assert.That(layer!.SkinRetracted, Is.True, "Skin should be retracted after DoAfter");

            var totalEv = new IntegrityPenaltyTotalRequestEvent(patient);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref totalEv);
            Assert.That(totalEv.Total, Is.GreaterThanOrEqualTo(1), "Integrity penalty should be at least 1 after retract skin (CreateIncision+ClampVessels+RetractSkin)");
        });
    }
}
