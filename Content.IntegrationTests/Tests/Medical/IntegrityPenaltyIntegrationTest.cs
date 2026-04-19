using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Integrity.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Integration tests for the integrity penalty system.
/// </summary>
[TestFixture]
[TestOf(typeof(IntegrityPenaltyAggregatorSystem))]
public sealed class IntegrityPenaltyIntegrationTest
{
    [Test]
    public async Task BodyPartQuery_ReturnsBodyParts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var ev = new BodyPartQueryEvent(human);
            entityManager.EventBus.RaiseLocalEvent(human, ref ev);

            Assert.That(ev.Parts, Has.Count.EqualTo(6), "Human should have 6 body parts (torso, head, 2 arms, 2 legs)");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IntegrityPenalty_StoresAndClears()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);

            var applyEv = new IntegrityPenaltyAppliedEvent(human, 2, "dirty room", IntegrityPenaltyCategory.DirtyRoom);
            entityManager.EventBus.RaiseLocalEvent(human, ref applyEv);

            var totalEv = new IntegrityPenaltyTotalRequestEvent(human);
            entityManager.EventBus.RaiseLocalEvent(human, ref totalEv);
            Assert.That(totalEv.Total, Is.EqualTo(2), "Total penalty should be 2 after applying");

            var clearEv = new IntegrityPenaltyClearedEvent(human, IntegrityPenaltyCategory.DirtyRoom);
            entityManager.EventBus.RaiseLocalEvent(human, ref clearEv);

            totalEv = new IntegrityPenaltyTotalRequestEvent(human);
            entityManager.EventBus.RaiseLocalEvent(human, ref totalEv);
            Assert.That(totalEv.Total, Is.EqualTo(0), "Total penalty should be 0 after clearing");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BodyPartPenalty_StoresAndAggregates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);

            var queryEv = new BodyPartQueryEvent(human);
            entityManager.EventBus.RaiseLocalEvent(human, ref queryEv);
            Assert.That(queryEv.Parts, Is.Not.Empty, "Human should have body parts");
            var torso = queryEv.Parts[0];

            var applyEv = new SurgeryPenaltyAppliedEvent(torso, 3);
            entityManager.EventBus.RaiseLocalEvent(torso, ref applyEv);

            var totalEv = new IntegrityPenaltyTotalRequestEvent(human);
            entityManager.EventBus.RaiseLocalEvent(human, ref totalEv);
            Assert.That(totalEv.Total, Is.EqualTo(3), "Total penalty should be 3 after applying to torso");

            var removeEv = new SurgeryPenaltyRemovedEvent(torso, 3);
            entityManager.EventBus.RaiseLocalEvent(torso, ref removeEv);

            totalEv = new IntegrityPenaltyTotalRequestEvent(human);
            entityManager.EventBus.RaiseLocalEvent(human, ref totalEv);
            Assert.That(totalEv.Total, Is.EqualTo(0), "Total penalty should be 0 after removing");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IntegrityPenaltyTotal_CombinesPartAndContextual()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);

            var queryEv = new BodyPartQueryEvent(human);
            entityManager.EventBus.RaiseLocalEvent(human, ref queryEv);
            var torso = queryEv.Parts[0];

            var surgeryApplyEv = new SurgeryPenaltyAppliedEvent(torso, 2);
            entityManager.EventBus.RaiseLocalEvent(torso, ref surgeryApplyEv);

            var integrityApplyEv = new IntegrityPenaltyAppliedEvent(human, 3, "improper tools", IntegrityPenaltyCategory.ImproperTools);
            entityManager.EventBus.RaiseLocalEvent(human, ref integrityApplyEv);

            var totalEv = new IntegrityPenaltyTotalRequestEvent(human);
            entityManager.EventBus.RaiseLocalEvent(human, ref totalEv);
            Assert.That(totalEv.Total, Is.EqualTo(5), "Total penalty should be 5 (2 from part + 3 contextual)");
        });

        await pair.CleanReturnAsync();
    }

}
