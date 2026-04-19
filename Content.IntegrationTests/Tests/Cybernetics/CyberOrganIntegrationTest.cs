using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Surgery.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(CyberOrganComponent))]
public sealed class CyberOrganIntegrationTest
{
    private static EntityUid GetTorso(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Torso") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static EntityUid GetHeart(IEntityManager entityManager, BodySystem bodySystem, EntityUid body)
    {
        return bodySystem.GetAllOrgans(body).First(o =>
            entityManager.TryGetComponent(o, out OrganComponent? comp) && comp.Category?.Id == "Heart");
    }

    [Test]
    public async Task CyberOrgans_ExcludedFromCyberLimbTotals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso = GetTorso(entityManager, human);
            var heart = GetHeart(entityManager, bodySystem, human);

            var removeEv = new OrganRemoveRequestEvent(heart);
            entityManager.EventBus.RaiseLocalEvent(heart, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove heart should succeed");

            var cyberHeart = entityManager.SpawnEntity("OrganCyberHeartBasic", entityManager.GetComponent<TransformComponent>(human).Coordinates);
            var insertEv = new OrganInsertRequestEvent(torso, cyberHeart);
            entityManager.EventBus.RaiseLocalEvent(torso, ref insertEv);
            Assert.That(insertEv.Success, Is.True, "Insert cyber heart should succeed");

            Assert.That(entityManager.HasComponent<CyberLimbStatsComponent>(human), Is.False,
                "Body with only cyber organs should NOT have CyberLimbStatsComponent");
            Assert.That(entityManager.HasComponent<CyberneticsMaintenanceComponent>(human), Is.False,
                "Body with only cyber organs should NOT have CyberneticsMaintenanceComponent");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyberOrgans_CountTowardIntegrity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso = GetTorso(entityManager, human);
            var heart = GetHeart(entityManager, bodySystem, human);

            var removeEv = new OrganRemoveRequestEvent(heart);
            entityManager.EventBus.RaiseLocalEvent(heart, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove heart should succeed");

            var cyberHeart = entityManager.SpawnEntity("OrganCyberHeartBasic", entityManager.GetComponent<TransformComponent>(human).Coordinates);
            var insertEv = new OrganInsertRequestEvent(torso, cyberHeart);
            entityManager.EventBus.RaiseLocalEvent(torso, ref insertEv);
            Assert.That(insertEv.Success, Is.True, "Insert cyber heart should succeed");

            Assert.That(entityManager.TryGetComponent(human, out IntegrityUsageComponent? usageComp), Is.True,
                "Body should have IntegrityUsageComponent after inserting cyber organ");
            Assert.That(usageComp!.Usage, Is.EqualTo(1), "Usage should be 1 after inserting cyber heart with integrityCost 1");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyberOrgan_Insertion_SucceedsWithCorrectEffectiveness()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso = GetTorso(entityManager, human);
            var heart = GetHeart(entityManager, bodySystem, human);

            var removeEv = new OrganRemoveRequestEvent(heart);
            entityManager.EventBus.RaiseLocalEvent(heart, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove heart should succeed");

            var cyberHeart = entityManager.SpawnEntity("OrganCyberHeartBasic", entityManager.GetComponent<TransformComponent>(human).Coordinates);
            Assert.That(entityManager.TryGetComponent(cyberHeart, out CyberOrganComponent? cyberComp), Is.True,
                "Cyber heart should have CyberOrganComponent");
            Assert.That(cyberComp!.Effectiveness, Is.EqualTo(0.8f), "Basic cyber heart should have 80% effectiveness");

            var insertEv = new OrganInsertRequestEvent(torso, cyberHeart);
            entityManager.EventBus.RaiseLocalEvent(torso, ref insertEv);
            Assert.That(insertEv.Success, Is.True, "Insert cyber heart should succeed");

            var insertedHeart = bodySystem.GetAllOrgans(human).First(o =>
                entityManager.TryGetComponent(o, out OrganComponent? oc) && oc.Category?.Id == "Heart");
            Assert.That(entityManager.TryGetComponent(insertedHeart, out CyberOrganComponent? insertedCyber), Is.True,
                "Inserted organ should have CyberOrganComponent");
            Assert.That(insertedCyber!.Effectiveness, Is.EqualTo(0.8f), "Inserted cyber heart should retain 80% effectiveness");
        });

        await pair.CleanReturnAsync();
    }
}
