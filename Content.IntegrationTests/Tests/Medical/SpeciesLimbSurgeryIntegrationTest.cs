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

[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class SpeciesLimbSurgeryIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static readonly string[] SpeciesMobPrototypes =
    [
        "MobHuman", "MobDiona", "MobVox", "MobMoth", "MobVulpkanin",
        "MobReptilian", "MobArachnid", "MobDwarf", "MobGingerbread",
        "MobSlimePerson", "MobSkeletonPerson", "MobAvali",
    ];

    private static EntityUid GetBodyPart(IEntityManager entityManager, EntityUid body, string category)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0), $"Body should have {category}");
        return ev.Parts[0];
    }

    [Test, TestCaseSource(nameof(SpeciesMobPrototypes))]
    public async Task Species_ArmDetachAndReattach_Succeeds(string mobProto)
    {
        await TestLimbDetachAndReattach(mobProto, "ArmLeft");
    }

    [Test, TestCaseSource(nameof(SpeciesMobPrototypes))]
    public async Task Species_LegDetachAndReattach_Succeeds(string mobProto)
    {
        await TestLimbDetachAndReattach(mobProto, "LegLeft");
    }

    private async Task TestLimbDetachAndReattach(string mobProto, string limbCategory)
    {
        await SpawnTarget(mobProto);
        var patient = STarget!.Value;
        var patientNet = Target!.Value;

        var analyzerNet = NetEntity.Invalid;
        var scalpelNet = NetEntity.Invalid;
        var wirecutterNet = NetEntity.Invalid;
        var retractorNet = NetEntity.Invalid;
        var sawNet = NetEntity.Invalid;
        var cauteryNet = NetEntity.Invalid;
        var limbNet = NetEntity.Invalid;
        await Server.WaitPost(() =>
        {
            var analyzer = SEntMan.SpawnEntity("HandheldHealthAnalyzer", SEntMan.GetCoordinates(TargetCoords));
            var scalpel = SEntMan.SpawnEntity("Scalpel", SEntMan.GetCoordinates(TargetCoords));
            var wirecutter = SEntMan.SpawnEntity("Wirecutter", SEntMan.GetCoordinates(TargetCoords));
            var retractor = SEntMan.SpawnEntity("Retractor", SEntMan.GetCoordinates(TargetCoords));
            var saw = SEntMan.SpawnEntity("Saw", SEntMan.GetCoordinates(TargetCoords));
            var cautery = SEntMan.SpawnEntity("Cautery", SEntMan.GetCoordinates(TargetCoords));
            var limb = GetBodyPart(SEntMan, patient, limbCategory);

            HandSys.TryPickupAnyHand(SPlayer, analyzer, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, scalpel, checkActionBlocker: false);

            analyzerNet = SEntMan.GetNetEntity(analyzer);
            scalpelNet = SEntMan.GetNetEntity(scalpel);
            wirecutterNet = SEntMan.GetNetEntity(wirecutter);
            retractorNet = SEntMan.GetNetEntity(retractor);
            sawNet = SEntMan.GetNetEntity(saw);
            cauteryNet = SEntMan.GetNetEntity(cautery);
            limbNet = SEntMan.GetNetEntity(limb);
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
        Assert.That(IsUiOpen(HealthAnalyzerUiKey.Key), Is.True);

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

        var skipSkinTissue = mobProto == "MobSkeletonPerson";
        if (!skipSkinTissue)
        {
            await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, limbNet, "CreateIncision", SurgeryLayer.Skin, false), analyzerNet);
            await AwaitDoAfters(maxExpected: 1);
            await Server.WaitPost(() => { HandSys.TryDrop((SPlayer, Hands!), null, false); HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(wirecutterNet), false); });
            await RunTicks(1);
            await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, limbNet, "ClampVessels", SurgeryLayer.Skin, false), analyzerNet);
            await AwaitDoAfters(maxExpected: 1);
            await Server.WaitPost(() => { HandSys.TryDrop((SPlayer, Hands!), null, false); HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), false); });
            await RunTicks(1);
            await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, limbNet, "RetractSkin", SurgeryLayer.Skin, false), analyzerNet);
            await AwaitDoAfters(maxExpected: 1);
            await Server.WaitPost(() => { HandSys.TryDrop((SPlayer, Hands!), null, false); HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(sawNet), false); });
            await RunTicks(1);
            await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, limbNet, "CutBone", SurgeryLayer.Tissue, false), analyzerNet);
            await AwaitDoAfters(maxExpected: 1);
            await Server.WaitPost(() => { HandSys.TryDrop((SPlayer, Hands!), null, false); HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(cauteryNet), false); });
            await RunTicks(1);
            await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, limbNet, "MarrowBleeding", SurgeryLayer.Tissue, false), analyzerNet);
            await AwaitDoAfters(maxExpected: 1);
            await Server.WaitPost(() => { HandSys.TryDrop((SPlayer, Hands!), null, false); HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), false); });
            await RunTicks(1);
            await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, limbNet, "RetractTissue", SurgeryLayer.Tissue, false), analyzerNet);
            await AwaitDoAfters(maxExpected: 1);
            await Server.WaitPost(() => { HandSys.TryDrop((SPlayer, Hands!), null, false); HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(scalpelNet), false); });
            await RunTicks(1);
        }

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, limbNet, "DetachLimb", SurgeryLayer.Organ, false), analyzerNet);
        await AwaitDoAfters(maxExpected: 1);
        await RunTicks(15);

        await Server.WaitAssertion(() =>
        {
            var limbEnt = SEntMan.GetEntity(limbNet);
            Assert.That(SEntMan.EntityExists(limbEnt), Is.True);
            if (SEntMan.TryGetComponent(limbEnt, out BodyPartComponent? bp))
                Assert.That(bp.Body, Is.Null);
            else if (SEntMan.TryGetComponent(limbEnt, out OrganComponent? oc))
                Assert.That(oc.Body, Is.Null);
        });

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), null, false);
            Assert.That(HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(limbNet), false), Is.True);
        });
        await RunTicks(1);

        await Server.WaitPost(() =>
        {
            var limbUid = SEntMan.GetEntity(limbNet);
            HandSys.TryDrop((SPlayer, Hands!), null, false);
            var bodyComp = SEntMan.GetComponent<BodyComponent>(patient);
            var containerSys = SEntMan.System<SharedContainerSystem>();
            Assert.That(bodyComp.Organs, Is.Not.Null);
            Assert.That(containerSys.Insert(limbUid, bodyComp.Organs!), Is.True);
        });
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var limbEnt = SEntMan.GetEntity(limbNet);
            Assert.That(SEntMan.EntityExists(limbEnt), Is.True);
            if (SEntMan.TryGetComponent(limbEnt, out BodyPartComponent? bp))
                Assert.That(bp.Body, Is.EqualTo(patient));
            else if (SEntMan.TryGetComponent(limbEnt, out OrganComponent? oc))
                Assert.That(oc.Body, Is.EqualTo(patient));
        });
    }
}
