using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
public sealed class OrganBodyComponentsTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: OrganBodyComponentsTestBody
  components:
  - type: Body

- type: entity
  id: OrganBodyComponentsTestOrgan
  components:
  - type: Organ
  - type: OrganBodyComponents
    components:
    - type: PressureImmunity
";

    [Test]
    public async Task OrganInsertGrantsAndRemoveStripsBodyComponents()
    {
        var pair = Pair;
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var containerSys = entityManager.System<SharedContainerSystem>();

            var body = entityManager.SpawnEntity("OrganBodyComponentsTestBody", mapData.GridCoords);
            Assert.That(entityManager.HasComponent<BodyComponent>(body), Is.True);
            Assert.That(entityManager.HasComponent<PressureImmunityComponent>(body), Is.False);

            var organ = entityManager.SpawnInContainerOrDrop("OrganBodyComponentsTestOrgan", body, BodyComponent.ContainerID);

            Assert.That(entityManager.HasComponent<PressureImmunityComponent>(body), Is.True);

            var bodyOrgans = containerSys.GetContainer(body, BodyComponent.ContainerID);
            containerSys.Remove(organ, bodyOrgans);

            Assert.That(entityManager.HasComponent<PressureImmunityComponent>(body), Is.False);
        });
    }
}
