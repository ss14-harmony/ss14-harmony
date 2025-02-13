using Content.Server._Harmony.Maps.Additions;
using Content.Server._Harmony.Maps.Additions.Systems;
using Content.Server.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Harmony.Maps.Additions;

[TestFixture]
public sealed class MapAdditionsTests
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: MapAdditionTestEntity

- type: mapAddition
  id: TestingMapAddition
  entities:
  - prototype: MapAdditionTestEntity
    name: TEST ENTITY NAME
    description: TEST ENTITY DESCRIPTION
    position: 0,0
";

    /// <summary>
    /// Checks that the map addition will correctly add the needed entity
    /// </summary>
    [Test]
    public async Task AddsDummyTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();

        var mapAdditionsSystem = entityManager.EntitySysManager.GetEntitySystem<MapAdditionSystem>();

        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            entityManager.EnsureComponent<BecomesStationComponent>(testMap.Grid); // required for the map addition to work.

            mapAdditionsSystem.ApplyMapAddition(prototypeManager.Index<MapAdditionPrototype>("TestingMapAddition"),
                testMap.MapId);

            var entities = entityManager.GetEntities();

            var foundEntity = entities.FirstOrNull(uid =>
                entityManager.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "MapAdditionTestEntity");

            Assert.That(foundEntity, Is.Not.EqualTo(null), "Entity was not added!");

            var metaData = entityManager.GetComponent<MetaDataComponent>(foundEntity!.Value);

            Assert.Multiple(() =>
            {
                Assert.That(metaData.EntityName, Is.EqualTo("TEST ENTITY NAME"), "Name was not set correctly!");
                Assert.That(metaData.EntityDescription, Is.EqualTo("TEST ENTITY DESCRIPTION"), "Description was not set correctly!");
            });
        });

        await pair.CleanReturnAsync();
    }
}
