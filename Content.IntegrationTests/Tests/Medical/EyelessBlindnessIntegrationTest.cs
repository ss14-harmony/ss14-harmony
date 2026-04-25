#nullable enable
using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Eye.Blinding.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

#nullable enable
namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(EyelessBlindnessComponent))]
public sealed class EyelessBlindnessIntegrationTest
{
    private static EntityUid GetHead(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Head") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static EntityUid GetEyes(IEntityManager entityManager, BodySystem bodySystem, EntityUid body)
    {
        return bodySystem.GetAllOrgans(body).First(o =>
            entityManager.TryGetComponent(o, out OrganComponent? comp) && comp.Category?.Id == "Eyes");
    }

    [Test]
    public async Task RemoveEyes_AddsEyelessBlindness_AndInsertRestoresSight()
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
            var head = GetHead(entityManager, human);
            var eyes = GetEyes(entityManager, bodySystem, human);

            Assert.That(entityManager.TryGetComponent(human, out BlindableComponent? blindable), Is.True,
                "Human should have BlindableComponent");

            var removeEv = new OrganRemoveRequestEvent(eyes);
            entityManager.EventBus.RaiseLocalEvent(eyes, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove eyes should succeed");

            Assert.That(entityManager.HasComponent<EyelessBlindnessComponent>(human), Is.True,
                "Body should gain EyelessBlindnessComponent when eyes are removed");
            Assert.That(blindable!.IsBlind, Is.True, "Mob should be blind without eyes");

            var insertEv = new OrganInsertRequestEvent(head, eyes);
            entityManager.EventBus.RaiseLocalEvent(head, ref insertEv);
            Assert.That(insertEv.Success, Is.True, "Re-insert eyes should succeed");

            Assert.That(entityManager.HasComponent<EyelessBlindnessComponent>(human), Is.False,
                "EyelessBlindnessComponent should be removed when eyes are back");
            Assert.That(blindable.IsBlind, Is.False, "Mob should see again with eyes installed");
        });

        await pair.CleanReturnAsync();
    }
}
