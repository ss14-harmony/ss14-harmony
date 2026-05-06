using System.Linq;
using Content.IntegrationTests;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Creature surgery smoke: exactly one torso body part on monkey and heart explants cleanly.
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class CreatureSurgeryIntegrationTest
{
    private static EntityUid GetHeart(IEntityManager entityManager, BodySystem bodySystem, EntityUid body)
    {
        return bodySystem.GetAllOrgans(body).First(o =>
            entityManager.TryGetComponent(o, out OrganComponent? comp) && comp.Category?.Id == "Heart");
    }

    [Test]
    public async Task MonkeyCreature_SingleTorso_HeartExplant_RemovedFromBody()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var monkey = entityManager.SpawnEntity("MobMonkey", mapData.GridCoords);

            var partEv = new BodyPartQueryEvent(monkey);
            entityManager.EventBus.RaiseLocalEvent(monkey, ref partEv);
            Assert.That(partEv.Parts, Has.Count.EqualTo(1), "Creature surgery mob should have exactly one body part");

            var heart = GetHeart(entityManager, bodySystem, monkey);
            var coords = entityManager.GetComponent<TransformComponent>(monkey).Coordinates;
            var removeEv = new OrganRemoveRequestEvent(heart) { Destination = coords };
            entityManager.EventBus.RaiseLocalEvent(heart, ref removeEv);
            Assert.That(removeEv.Success, Is.True);

            Assert.That(entityManager.GetComponent<OrganComponent>(heart).Body, Is.Null,
                "Explanted monkey heart should no longer be attached to the body");
            Assert.That(bodySystem.GetAllOrgans(monkey).Contains(heart), Is.False,
                "Heart should not enumerate under monkey body after explant");
        });

        await pair.CleanReturnAsync();
    }
}
