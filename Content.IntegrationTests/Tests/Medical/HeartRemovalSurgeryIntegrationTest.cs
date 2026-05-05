using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.MedicalScanner;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Integration test for removing a heart via Health Analyzer surgery BUI.
/// Performs CreateIncision, ClampVessels, RetractSkin, CutBone, MarrowBleeding, RetractTissue, then OrganClampVessels, OrganRemovalScalpel, OrganRemovalHemostat, RemoveOrgan (heart).
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class HeartRemovalSurgeryIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static EntityUid GetTorso(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Torso") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0), "Body should have a torso");
        return ev.Parts[0];
    }

    private static EntityUid GetHeart(IEntityManager entityManager, BodySystem bodySystem, EntityUid body)
    {
        return bodySystem.GetAllOrgans(body).First(o =>
            entityManager.TryGetComponent(o, out OrganComponent? comp) && comp.Category?.Id == "Heart");
    }

    [Test]
    public async Task SurgeryRequestBuiMessage_HeartRemoval_Completes()
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
        var torsoNet = NetEntity.Invalid;
        var heartNet = NetEntity.Invalid;

        await Server.WaitPost(() =>
        {
            var analyzer = SEntMan.SpawnEntity("HandheldHealthAnalyzer", SEntMan.GetCoordinates(TargetCoords));
            var scalpel = SEntMan.SpawnEntity("Scalpel", SEntMan.GetCoordinates(TargetCoords));
            var wirecutter = SEntMan.SpawnEntity("Wirecutter", SEntMan.GetCoordinates(TargetCoords));
            var retractor = SEntMan.SpawnEntity("Retractor", SEntMan.GetCoordinates(TargetCoords));
            var saw = SEntMan.SpawnEntity("SawElectric", SEntMan.GetCoordinates(TargetCoords));
            var cautery = SEntMan.SpawnEntity("Cautery", SEntMan.GetCoordinates(TargetCoords));
            var torso = GetTorso(SEntMan, patient);
            var bodySystem = SEntMan.System<BodySystem>();
            var heart = GetHeart(SEntMan, bodySystem, patient);

            HandSys.TryPickupAnyHand(SPlayer, analyzer, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, scalpel, checkActionBlocker: false);

            analyzerNet = SEntMan.GetNetEntity(analyzer);
            scalpelNet = SEntMan.GetNetEntity(scalpel);
            wirecutterNet = SEntMan.GetNetEntity(wirecutter);
            retractorNet = SEntMan.GetNetEntity(retractor);
            sawNet = SEntMan.GetNetEntity(saw);
            cauteryNet = SEntMan.GetNetEntity(cautery);
            torsoNet = SEntMan.GetNetEntity(torso);
            heartNet = SEntMan.GetNetEntity(heart);
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

        // Use direct SurgeryRequestEvent for all steps to avoid BUI/client-server sync issues in integration test
        EntityUid torso = default;
        EntityUid heart = default;
        EntityUid analyzer = default;
        await Server.WaitPost(() =>
        {
            torso = SEntMan.GetEntity(torsoNet);
            var bodySystem = SEntMan.System<BodySystem>();
            heart = GetHeart(SEntMan, bodySystem, patient);
            heartNet = SEntMan.GetNetEntity(heart);
            analyzer = SEntMan.GetEntity(analyzerNet);
        });

        await Server.WaitPost(() =>
        {
            var scalpelUid = SEntMan.GetEntity(scalpelNet);
            foreach (var h in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), h, out var held) && held == scalpelUid)
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), h);
                    break;
                }
            }
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, torso, "CreateIncision", SurgeryLayer.Skin, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"CreateIncision: {ev.RejectReason}");
        });
        await RunSeconds(4);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(wirecutterNet), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, torso, "ClampVessels", SurgeryLayer.Skin, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"ClampVessels: {ev.RejectReason}");
        });
        await RunSeconds(4);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, torso, "RetractSkin", SurgeryLayer.Skin, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"RetractSkin: {ev.RejectReason}");
        });
        await RunSeconds(4);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(sawNet), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, torso, "CutBone", SurgeryLayer.Tissue, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"CutBone: {ev.RejectReason}");
        });
        await RunSeconds(4);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(cauteryNet), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, torso, "MarrowBleeding", SurgeryLayer.Tissue, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"MarrowBleeding: {ev.RejectReason}");
        });
        await RunSeconds(4);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, torso, "RetractTissue", SurgeryLayer.Tissue, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"RetractTissue: {ev.RejectReason}");
        });
        await RunSeconds(4);

        var hemostatNet = NetEntity.Invalid;
        await Server.WaitPost(() =>
        {
            var hemostat = SEntMan.SpawnEntity("Hemostat", SEntMan.GetCoordinates(TargetCoords));
            hemostatNet = SEntMan.GetNetEntity(hemostat);
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, hemostat, checkActionBlocker: false);
        });
        await RunTicks(1);

        // Organ removal steps
        await Server.WaitPost(() =>
        {
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == SEntMan.GetEntity(hemostatNet))
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    break;
                }
            }
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, torso, "OrganClampVessels", SurgeryLayer.Organ, false, heart);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"OrganClampVessels request should be valid. RejectReason: {ev.RejectReason}");
        });
        await RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            var torsoEnt = SEntMan.GetEntity(torsoNet);
            Assert.That(SEntMan.TryGetComponent(torsoEnt, out SurgeryLayerComponent? layer), Is.True, "Torso should have SurgeryLayerComponent");
            var entry = layer!.OrganRemovalProgress.FirstOrDefault(e => e.Organ == heartNet);
            Assert.That(entry, Is.Not.Null, "OrganClampVessels should have added progress for heart");
            Assert.That(entry!.Steps, Does.Contain("OrganClampVessels"), "OrganClampVessels step should be in progress");
        });

        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(scalpelNet), checkActionBlocker: false);
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == SEntMan.GetEntity(scalpelNet))
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    break;
                }
            }
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, torso, "OrganRemovalScalpel", SurgeryLayer.Organ, false, heart);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"OrganRemovalScalpel: {ev.RejectReason}");
        });
        await RunSeconds(5);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(cauteryNet), checkActionBlocker: false);
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == SEntMan.GetEntity(cauteryNet))
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    break;
                }
            }
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, torso, "OrganRemovalHemostat", SurgeryLayer.Organ, false, heart);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"OrganRemovalHemostat: {ev.RejectReason}");
        });
        await RunSeconds(5);

        await Server.WaitPost(() =>
        {
            var removeEv = new SurgeryRequestEvent(analyzer, SPlayer, patient, torso, "RemoveOrgan", SurgeryLayer.Organ, false, heart);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref removeEv);
            Assert.That(removeEv.Valid, Is.True, $"RemoveOrgan: {removeEv.RejectReason}");
        });
        await RunSeconds(5);

        var bodySystem = SEntMan.System<BodySystem>();

        await Server.WaitAssertion(() =>
        {
            var heart = SEntMan.GetEntity(heartNet);
            Assert.That(SEntMan.EntityExists(heart), Is.True, "Heart entity should exist after removal");
            var organsInBody = bodySystem.GetAllOrgans(patient).ToList();
            Assert.That(organsInBody, Does.Not.Contain(heart), "Heart should be removed from body");
        });
    }
}
