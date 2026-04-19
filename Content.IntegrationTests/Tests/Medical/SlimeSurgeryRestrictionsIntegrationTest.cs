using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Integration test for slime surgery restrictions.
/// Slimes cannot receive organ or limb implants (design doc: Slime-Specific Systems).
/// </summary>
[TestFixture]
[TestOf(typeof(SurgerySystem))]
public sealed class SlimeSurgeryRestrictionsIntegrationTest
{
    private static EntityUid GetTorso(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Torso") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0), "Body should have a torso");
        return ev.Parts[0];
    }

    private static EntityUid GetLeg(IEntityManager entityManager, EntityUid body, string category = "LegLeft")
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0), $"Body should have a {category}");
        return ev.Parts[0];
    }

    [Test]
    public async Task SlimePerson_InsertOrgan_Rejected()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var coords = mapData.GridCoords;
            var slime = entityManager.SpawnEntity("MobSlimePerson", coords);
            var surgeon = entityManager.SpawnEntity("MobHuman", coords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", coords);
            var torso = GetTorso(entityManager, slime);
            var heart = entityManager.SpawnEntity("OrganHumanHeart", coords);

            var ev = new SurgeryRequestEvent(
                analyzer,
                surgeon,
                slime,
                torso,
                (ProtoId<SurgeryProcedurePrototype>)"InsertOrgan",
                SurgeryLayer.Organ,
                false,
                heart);

            entityManager.EventBus.RaiseLocalEvent(slime, ref ev);

            Assert.That(ev.Valid, Is.False, "InsertOrgan should be rejected for slime");
            Assert.That(ev.RejectReason, Is.EqualTo("slime-cannot-receive-implants"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SlimePerson_AttachLimb_Rejected()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var coords = mapData.GridCoords;
            var slime = entityManager.SpawnEntity("MobSlimePerson", coords);
            var surgeon = entityManager.SpawnEntity("MobHuman", coords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", coords);

            var leg = GetLeg(entityManager, slime);
            var bodyComp = entityManager.GetComponent<BodyComponent>(slime);
            var removeEv = new OrganRemoveRequestEvent(leg) { Destination = coords };
            entityManager.EventBus.RaiseLocalEvent(leg, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove leg should succeed");

            var cyberLeg = entityManager.SpawnEntity("OrganCyberLegLeft", coords);

            var ev = new SurgeryRequestEvent(
                analyzer,
                surgeon,
                slime,
                slime,
                (ProtoId<SurgeryProcedurePrototype>)"AttachLimb",
                SurgeryLayer.Organ,
                false,
                cyberLeg);

            entityManager.EventBus.RaiseLocalEvent(slime, ref ev);

            Assert.That(ev.Valid, Is.False, "AttachLimb should be rejected for slime");
            Assert.That(ev.RejectReason, Is.EqualTo("slime-cannot-receive-implants"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Human_AttachLimb_NotRejectedForSlimeReason()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var coords = mapData.GridCoords;
            var human = entityManager.SpawnEntity("MobHuman", coords);
            var surgeon = entityManager.SpawnEntity("MobHuman", coords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", coords);

            var leg = GetLeg(entityManager, human);
            var removeEv = new OrganRemoveRequestEvent(leg) { Destination = coords };
            entityManager.EventBus.RaiseLocalEvent(leg, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove leg should succeed");

            var cyberLeg = entityManager.SpawnEntity("OrganCyberLegLeft", coords);

            var ev = new SurgeryRequestEvent(
                analyzer,
                surgeon,
                human,
                human,
                (ProtoId<SurgeryProcedurePrototype>)"AttachLimb",
                SurgeryLayer.Organ,
                false,
                cyberLeg);

            entityManager.EventBus.RaiseLocalEvent(human, ref ev);

            Assert.That(ev.RejectReason, Is.Not.EqualTo("slime-cannot-receive-implants"),
                "Human should not be rejected for slime restriction");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SlimePerson_DetachLimb_NotRejectedForSlimeReason()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var coords = mapData.GridCoords;
            var slime = entityManager.SpawnEntity("MobSlimePerson", coords);
            var surgeon = entityManager.SpawnEntity("MobHuman", coords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", coords);
            var leg = GetLeg(entityManager, slime);

            var ev = new SurgeryRequestEvent(
                analyzer,
                surgeon,
                slime,
                leg,
                (ProtoId<SurgeryProcedurePrototype>)"DetachLimb",
                SurgeryLayer.Organ,
                false,
                null);

            entityManager.EventBus.RaiseLocalEvent(slime, ref ev);

            Assert.That(ev.RejectReason, Is.Not.EqualTo("slime-cannot-receive-implants"),
                "DetachLimb should not be rejected for slime restriction (may fail for layer-not-open)");
        });

        await pair.CleanReturnAsync();
    }
}
