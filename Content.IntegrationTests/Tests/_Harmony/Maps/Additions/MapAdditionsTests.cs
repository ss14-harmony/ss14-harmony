using System.Linq;
using Robust.Shared.GameObjects;
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
  applyOn: Empty
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
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Map = "Empty", DummyTicker = false });
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
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
