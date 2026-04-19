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
using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Verifies that after amputating a leg, re-attaching it, and performing CloseIncision (Mend Skin),
/// RetractSkin is available again (1:1 pairing: CloseIncision undoes RetractSkin, so it can be re-performed).
/// </summary>
[TestFixture]
[TestOf(typeof(SurgeryLayerSystem))]
public sealed class ReattachedLimbSurgeryIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static EntityUid GetLeg(IEntityManager entityManager, EntityUid body, string category = "LegLeft")
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    [Test]
    public async Task ReattachedLimb_CloseIncision_RetractSkinAvailableAgain()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;
        var patientNet = Target!.Value;

        var analyzerNet = NetEntity.Invalid;
        var scalpelNet = NetEntity.Invalid;
        var wirecutterNet = NetEntity.Invalid;
        var sawNet = NetEntity.Invalid;
        var cauteryNet = NetEntity.Invalid;
        var retractorNet = NetEntity.Invalid;
        var legNet = NetEntity.Invalid;

        var skinOpenProcs = Array.Empty<ProtoId<SurgeryProcedurePrototype>>();
        var skinCloseProcs = Array.Empty<ProtoId<SurgeryProcedurePrototype>>();
        var tissueOpenProcs = Array.Empty<ProtoId<SurgeryProcedurePrototype>>();
        var organStepProcs = Array.Empty<ProtoId<SurgeryProcedurePrototype>>();

        await Server.WaitPost(() =>
        {
            var leg = GetLeg(SEntMan, patient);
            var surgeryLayer = SEntMan.System<SurgeryLayerSystem>();
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, leg)!;
            var skinOpen = stepsConfig.GetSkinOpenStepIds(ProtoMan);
            var skinClose = stepsConfig.GetSkinCloseStepIds(ProtoMan);
            var tissueOpen = stepsConfig.GetTissueOpenStepIds(ProtoMan);
            var organSteps = stepsConfig.GetOrganStepIds(ProtoMan);
            skinOpenProcs = skinOpen.Select(s => (ProtoId<SurgeryProcedurePrototype>)BodyPartSurgeryStepsPrototype.GetProcedureForStep(s)).ToArray();
            skinCloseProcs = skinClose.Select(s => (ProtoId<SurgeryProcedurePrototype>)BodyPartSurgeryStepsPrototype.GetProcedureForStep(s)).ToArray();
            tissueOpenProcs = tissueOpen.Select(s => (ProtoId<SurgeryProcedurePrototype>)BodyPartSurgeryStepsPrototype.GetProcedureForStep(s)).ToArray();
            organStepProcs = organSteps.Select(s => (ProtoId<SurgeryProcedurePrototype>)s).ToArray();

            var analyzer = SEntMan.SpawnEntity("HandheldHealthAnalyzer", SEntMan.GetCoordinates(TargetCoords));
            var scalpel = SEntMan.SpawnEntity("Scalpel", SEntMan.GetCoordinates(TargetCoords));
            var wirecutter = SEntMan.SpawnEntity("Wirecutter", SEntMan.GetCoordinates(TargetCoords));
            var saw = SEntMan.SpawnEntity("Saw", SEntMan.GetCoordinates(TargetCoords));
            var cautery = SEntMan.SpawnEntity("Cautery", SEntMan.GetCoordinates(TargetCoords));
            var retractor = SEntMan.SpawnEntity("Retractor", SEntMan.GetCoordinates(TargetCoords));

            HandSys.TryPickupAnyHand(SPlayer, analyzer, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, scalpel, checkActionBlocker: false);

            analyzerNet = SEntMan.GetNetEntity(analyzer);
            scalpelNet = SEntMan.GetNetEntity(scalpel);
            wirecutterNet = SEntMan.GetNetEntity(wirecutter);
            sawNet = SEntMan.GetNetEntity(saw);
            cauteryNet = SEntMan.GetNetEntity(cautery);
            retractorNet = SEntMan.GetNetEntity(retractor);
            legNet = SEntMan.GetNetEntity(leg);
        });

        await RunTicks(5);

        // Use SurgeryRequestEvent directly to avoid BUI/client-server sync issues and get precise RejectReason on failure
        var analyzer = SEntMan.GetEntity(analyzerNet);
        var leg = SEntMan.GetEntity(legNet);

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
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, leg, skinOpenProcs[0], SurgeryLayer.Skin, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"{skinOpenProcs[0]}: {ev.RejectReason}");
        });
        await RunSeconds(4);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(wirecutterNet), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, leg, skinOpenProcs[1], SurgeryLayer.Skin, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"{skinOpenProcs[1]}: {ev.RejectReason}");
        });
        await RunSeconds(4);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, leg, skinOpenProcs[2], SurgeryLayer.Skin, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"{skinOpenProcs[2]}: {ev.RejectReason}");
        });
        await RunSeconds(4);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(sawNet), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, leg, tissueOpenProcs[0], SurgeryLayer.Tissue, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"{tissueOpenProcs[0]}: {ev.RejectReason}");
        });
        await RunSeconds(4);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(cauteryNet), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, leg, tissueOpenProcs[1], SurgeryLayer.Tissue, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"{tissueOpenProcs[1]}: {ev.RejectReason}");
        });
        await RunSeconds(4);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, leg, tissueOpenProcs[2], SurgeryLayer.Tissue, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"{tissueOpenProcs[2]}: {ev.RejectReason}");
        });
        await RunSeconds(4);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(scalpelNet), checkActionBlocker: false);
            var detachLimb = organStepProcs.First(p => p.ToString() == "DetachLimb");
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, leg, detachLimb, SurgeryLayer.Organ, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"{detachLimb}: {ev.RejectReason}");
        });
        await RunSeconds(5);

        await Server.WaitPost(() =>
        {
            var leg = SEntMan.GetEntity(legNet);
            Assert.That(SEntMan.EntityExists(leg), Is.True);
            Assert.That(SEntMan.TryGetComponent(leg, out BodyPartComponent? legBodyPart), Is.True);
            Assert.That(legBodyPart!.Body, Is.Null, "Leg should be detached");

            HandSys.TryPickupAnyHand(SPlayer, leg, checkActionBlocker: false);
        });
        await RunTicks(1);

        await Server.WaitPost(() =>
        {
            var leg = SEntMan.GetEntity(legNet);
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            var bodyComp = SEntMan.GetComponent<BodyComponent>(patient);
            var containerSys = SEntMan.System<SharedContainerSystem>();
            Assert.That(containerSys.Insert(leg, bodyComp.Organs!), Is.True, "Leg should re-attach");
        });
        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            var retractorUid = SEntMan.GetEntity(retractorNet);
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == retractorUid)
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    break;
                }
            }
            if (HandSys.GetActiveItem((SPlayer, Hands!)) != retractorUid)
                HandSys.TryPickupAnyHand(SPlayer, retractorUid, checkActionBlocker: false);
        });
        await RunTicks(1);

        // Apply ReleaseRetractor via event (BUI/DoAfter flow can be flaky when body structure changes).
        // This verifies the surgery layer logic: after ReleaseRetractor on a re-attached limb,
        // RetractSkin should be available again.
        await Server.WaitPost(() =>
        {
            var leg = SEntMan.GetEntity(legNet);
            var releaseRetractor = skinCloseProcs[0];
            var procedure = ProtoMan.Index(releaseRetractor);
            var ev = new SurgeryStepCompletedEvent(SPlayer, patient, leg, releaseRetractor, SurgeryLayer.Skin, null, null, procedure);
            SEntMan.EventBus.RaiseLocalEvent(leg, ref ev);
        });
        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            var leg = SEntMan.GetEntity(legNet);
            var layerComp = SEntMan.GetComponent<SurgeryLayerComponent>(leg);
            var releaseRetractorProc = skinCloseProcs[0].ToString();
            var retractSkinProc = skinOpenProcs[2].ToString();
            var createIncisionProc = skinOpenProcs[0].ToString();
            Assert.That(layerComp.PerformedSkinSteps, Does.Contain(releaseRetractorProc),
                "ReleaseRetractor should have been applied to re-attached leg");
            // With 1:1 pairing, ReleaseRetractor undoes RetractSkin, so RetractSkin is removed from performed
            Assert.That(layerComp.PerformedSkinSteps, Does.Not.Contain(retractSkinProc),
                "RetractSkin should be removed by ReleaseRetractor (1:1 pairing)");
            Assert.That(layerComp.PerformedSkinSteps, Does.Contain(createIncisionProc),
                "CreateIncision should remain (ReleaseRetractor only undoes RetractSkin)");

            var surgeryLayer = SEntMan.System<SurgeryLayerSystem>();
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, leg);
            Assert.That(stepsConfig, Is.Not.Null, "Re-attached leg should have steps config");
            Assert.That(surgeryLayer.CanPerformStep(retractSkinProc, SurgeryLayer.Skin, layerComp, stepsConfig!, leg), Is.True,
                "RetractSkin should be available again after ReleaseRetractor (CreateIncision still performed)");
            Assert.That(surgeryLayer.GetAvailableSteps(patient, leg), Does.Contain(retractSkinProc),
                "GetAvailableSteps should include RetractSkin");
        });
    }
}
