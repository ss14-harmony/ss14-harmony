using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(BodyPartOrganSystem))]
public sealed class OrganInsertRemoveIntegrationTest
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
    public async Task OrganRemove_ThenInsert_IntoSamePerson_Succeeds()
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
            Assert.That(removeEv.Success, Is.True, "Remove should succeed");

            var insertEv = new OrganInsertRequestEvent(torso, heart);
            entityManager.EventBus.RaiseLocalEvent(torso, ref insertEv);
            Assert.That(insertEv.Success, Is.True, "Insert should succeed");

            var organs = bodySystem.GetAllOrgans(human).ToList();
            Assert.That(organs, Does.Contain(heart), "Organ should be back in body");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganInsert_AlreadyInBody_Fails()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso = GetTorso(entityManager, human);
            var heart = GetHeart(entityManager, entityManager.System<BodySystem>(), human);

            var insertEv = new OrganInsertRequestEvent(torso, heart);
            entityManager.EventBus.RaiseLocalEvent(torso, ref insertEv);
            Assert.That(insertEv.Success, Is.False, "Insert of already-contained organ should fail");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganRemove_NotInBody_Fails()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var heart = entityManager.SpawnEntity("OrganHumanHeart", mapData.GridCoords);

            var removeEv = new OrganRemoveRequestEvent(heart);
            entityManager.EventBus.RaiseLocalEvent(heart, ref removeEv);
            Assert.That(removeEv.Success, Is.False, "Remove of organ not in body should fail");
        });

        await pair.CleanReturnAsync();
    }
}
