using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Shared.MedicalScanner;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

/// <summary>
/// Integration test for implanting a cyber arm and cutting it off via surgery.
/// Verifies the client does not crash when applying state during ResetPredictedEntities
/// (IntegrityUsage, CyberneticsMaintenance, CyberLimbStats component adds).
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class CyberArmImplantAndCutoffIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static EntityUid GetArmLeft(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("ArmLeft") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0), "Body should have ArmLeft");
        return ev.Parts[0];
    }

    private static void ReplaceArmWithCyberArm(IEntityManager entityManager, BodySystem bodySystem,
        SharedContainerSystem containerSystem, EntityUid body, EntityCoordinates coords)
    {
        var arm = GetArmLeft(entityManager, body);
        var removeEv = new OrganRemoveRequestEvent(arm) { Destination = coords };
        entityManager.EventBus.RaiseLocalEvent(arm, ref removeEv);
        Assert.That(removeEv.Success, Is.True, "Remove arm should succeed");

        var cyberArm = entityManager.SpawnEntity("OrganCyberArmLeft", coords);
        var bodyComp = entityManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have Organs container");
        Assert.That(containerSystem.Insert(cyberArm, bodyComp.Organs!), Is.True, "Insert cyber arm should succeed");
    }

    [Test]
    public async Task CyberArm_ImplantAndCutoff_DoesNotCrashClient()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;
        var patientNet = Target!.Value;

        var analyzerNet = NetEntity.Invalid;
        var scalpelNet = NetEntity.Invalid;
        var cyberArmNet = NetEntity.Invalid;

        var bodySystem = SEntMan.System<BodySystem>();
        var containerSystem = SEntMan.System<SharedContainerSystem>();

        await Server.WaitPost(() =>
        {
            var coords = MapData.GridCoords;
            var analyzer = SEntMan.SpawnEntity("HandheldHealthAnalyzer", coords);
            var scalpel = SEntMan.SpawnEntity("Scalpel", coords);
            ReplaceArmWithCyberArm(SEntMan, bodySystem, containerSystem, patient, coords);

            var cyberArm = bodySystem.GetAllOrgans(patient).First(o => SEntMan.HasComponent<CyberLimbComponent>(o));
            analyzerNet = SEntMan.GetNetEntity(analyzer);
            scalpelNet = SEntMan.GetNetEntity(scalpel);
            cyberArmNet = SEntMan.GetNetEntity(cyberArm);

            HandSys.TryPickupAnyHand(SPlayer, analyzer, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, scalpel, checkActionBlocker: false);
        });

        await RunTicks(5);

        // Use SurgeryRequestEvent directly to avoid BUI/client-server sync issues.
        // DetachLimb requires CuttingTool (scalpel) in hand.
        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(scalpelNet), checkActionBlocker: false);

            var analyzer = SEntMan.GetEntity(analyzerNet);
            var cyberArm = SEntMan.GetEntity(cyberArmNet);
            var ev = new SurgeryRequestEvent(analyzer, SPlayer, patient, cyberArm, (ProtoId<SurgeryProcedurePrototype>)"DetachLimb", SurgeryLayer.Organ, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, $"DetachLimb should succeed. RejectReason: {ev.RejectReason}");
        });
        await RunTicks(600);

        await Server.WaitAssertion(() =>
        {
            var cyberArm = SEntMan.GetEntity(cyberArmNet);
            Assert.That(SEntMan.EntityExists(cyberArm), Is.True, "Cyber arm entity should exist after detachment");
            Assert.That(SEntMan.TryGetComponent(cyberArm, out BodyPartComponent? armBodyPart), Is.True);
            Assert.That(armBodyPart!.Body, Is.Null, "Cyber arm should no longer be attached to body after DetachLimb");
        });

        // Run more ticks to ensure client applies state without crashing
        await RunTicks(50);
    }
}
