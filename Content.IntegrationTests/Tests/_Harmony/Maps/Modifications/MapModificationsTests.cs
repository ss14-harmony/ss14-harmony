using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Harmony.Maps.Modifications;
using Content.Server._Harmony.Maps.Modifications.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Harmony.Maps.Modifications;

[TestOf(typeof(MapModificationPrototype))]
public sealed class MapModificationsTests : GameTest
{
    private const string TestEntityToAdd = "TestEntityToAdd";
    private const string TestEntityToAddName = "TESTNAME1";
    private const string TestEntityToAddDescription = "TESTDESCRIPTION1";
    private const string TestEntityToRemove = "TestEntityToRemove";
    private const string TestModificationAddition = "TestAddition";
    private const string TestModificationRemoval = "TestRemoval";
    private const string TestModificationReplacement = "TestReplacement";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  id: {TestEntityToAdd}

- type: entity
  id: {TestEntityToRemove}

- type: mapModification
  id: {TestModificationAddition}
  additions:
  - prototype: {TestEntityToAdd}
    name: {TestEntityToAddName}
    description: {TestEntityToAddDescription}
    position: 1,0
    rotation: 90

- type: mapModification
  id: {TestModificationRemoval}
  removals:
  - !type:EntityPrototypeSelector
    prototype: {TestEntityToRemove}

- type: mapModification
  id: {TestModificationReplacement}
  replacements:
  - from:
    - !type:EntityPrototypeSelector
      prototype: {TestEntityToRemove}
    newPrototype: {TestEntityToAdd}
    newName: {TestEntityToAddName}
    newDescription: {TestEntityToAddDescription}
";

    [SidedDependency(Side.Server)] private readonly MapModificationSystem _mapModificationSystem = null!;

    /// <summary>
    /// Checks that map additions correctly add entities.
    /// </summary>
    [Test]
    public async Task TestAddition()
    {
        var testMap = await Pair.CreateTestMap();

        EntityUid? foundEntity = null;

        await Pair.Server.WaitPost(() =>
        {
            _mapModificationSystem.ApplyMapModification(
                SProtoMan.Index<MapModificationPrototype>(TestModificationAddition),
                testMap.Grid);

            foundEntity = SEntMan
                .GetEntities()
                .FirstOrNull(uid =>
                SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == TestEntityToAdd);
        });

        Assert.That(SEntMan.EntityExists(foundEntity), Is.True, "Entity was not added!");

        var metaData = SEntMan.GetComponent<MetaDataComponent>(foundEntity!.Value);
        var transform = SEntMan.GetComponent<TransformComponent>(foundEntity!.Value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                metaData.EntityName,
                Is.EqualTo(TestEntityToAddName),
                "Name was not set correctly!");
            Assert.That(
                metaData.EntityDescription,
                Is.EqualTo(TestEntityToAddDescription),
                "Description was not set correctly!");
            Assert.That(
                transform.LocalPosition,
                Is.EqualTo(new Vector2(1, 0)),
                "Position was not set correctly!");
            Assert.That(
                transform.LocalRotation,
                Is.EqualTo(new Angle(double.DegreesToRadians(90))),
                "Rotation was not set correctly!");
        }
    }

    /// <summary>
    /// Checks that map modifications will correctly remove an entity.
    /// </summary>
    [Test]
    public async Task TestRemoval()
    {
        var testMap = await Pair.CreateTestMap();

        EntityUid? foundEntity = null;

        await Pair.Server.WaitPost(() =>
        {
            SSpawnAtPosition(TestEntityToRemove, new EntityCoordinates(testMap.CGridUid, 0, 0));

            _mapModificationSystem.ApplyMapModification(
                SProtoMan.Index<MapModificationPrototype>(TestModificationRemoval),
                testMap.Grid);

            foundEntity = SEntMan
                .GetEntities()
                .FirstOrNull(uid =>
                    SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == TestEntityToRemove);
        });

        Assert.That(SEntMan.EntityExists(foundEntity), Is.False, "Entity was not deleted!");
    }

    /// <summary>
    /// Checks that map modifications will correctly replace an entity.
    /// </summary>
    [Test]
    public async Task TestReplacement()
    {
        var testMap = await Pair.CreateTestMap();

        EntityUid? foundToAdd = null;
        EntityUid? foundToRemove = null;

        await Pair.Server.WaitPost(() =>
        {
            SSpawnAtPosition(TestEntityToRemove, new EntityCoordinates(testMap.CGridUid, 0, 0));

            _mapModificationSystem.ApplyMapModification(
                SProtoMan.Index<MapModificationPrototype>(TestModificationReplacement),
                testMap.Grid);

            foreach (var uid in SEntMan.GetEntities())
            {
                if (foundToAdd == null &&
                    SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == TestEntityToAdd)
                {
                    foundToAdd = uid;
                }

                if (foundToRemove == null &&
                    SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == TestEntityToRemove)
                {
                    foundToRemove = uid;
                }
            }
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SEntMan.EntityExists(foundToAdd), Is.True, "Entity was not added!");
            Assert.That(SEntMan.EntityExists(foundToRemove), Is.False, "Entity was not removed!");
        }

        var metaData = SEntMan.GetComponent<MetaDataComponent>(foundToAdd!.Value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                metaData.EntityName,
                Is.EqualTo(TestEntityToAddName),
                "Name was not set correctly!");
            Assert.That(
                metaData.EntityDescription,
                Is.EqualTo(TestEntityToAddDescription),
                "Description was not set correctly!");
        }
    }
}
