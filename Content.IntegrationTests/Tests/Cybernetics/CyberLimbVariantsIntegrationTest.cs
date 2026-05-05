using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

/// <summary>
/// Integration tests for cyber limb variants: storage slot counts, military limb marker.
/// </summary>
[TestFixture]
[TestOf(typeof(CyberLimbStatsSystem))]
public sealed class CyberLimbVariantsIntegrationTest
{
    private static EntityUid GetArmLeft(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("ArmLeft") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static void ReplaceArmWithCyberArm(IEntityManager entityManager, BodySystem bodySystem,
        SharedContainerSystem containerSystem, EntityUid body, EntityCoordinates coords, string limbId)
    {
        var arm = GetArmLeft(entityManager, body);
        var removeEv = new OrganRemoveRequestEvent(arm) { Destination = coords };
        entityManager.EventBus.RaiseLocalEvent(arm, ref removeEv);
        Assert.That(removeEv.Success, Is.True, "Remove arm should succeed");

        var cyberArm = entityManager.SpawnEntity(limbId, coords);
        var bodyComp = entityManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have Organs container");
        Assert.That(containerSystem.Insert(cyberArm, bodyComp.Organs!), Is.True, "Insert cyber arm should succeed");
    }

    [Test]
    public async Task CyberArmVariants_HaveCorrectStorageSlotCounts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var basic = sEntMan.SpawnEntity("OrganCyberArmLeft", mapData.GridCoords);
            var t1 = sEntMan.SpawnEntity("OrganCyberArmLeftT1", mapData.GridCoords);
            var militaryT1 = sEntMan.SpawnEntity("OrganCyberArmLeftMilitaryT1", mapData.GridCoords);

            Assert.That(sEntMan.TryGetComponent(basic, out StorageComponent? basicStorage), Is.True);
            Assert.That(sEntMan.TryGetComponent(t1, out StorageComponent? t1Storage), Is.True);
            Assert.That(sEntMan.TryGetComponent(militaryT1, out StorageComponent? militaryT1Storage), Is.True);

            Assert.That(basicStorage!.Grid.GetArea(), Is.EqualTo(6), "Basic cyber arm should have 6 slots");
            Assert.That(t1Storage!.Grid.GetArea(), Is.EqualTo(8), "T1 cyber arm should have 8 slots");
            Assert.That(militaryT1Storage!.Grid.GetArea(), Is.EqualTo(6), "Military T1 cyber arm should have 6 slots");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MilitaryCyberArm_HasMarkerComponent_WhenAttachedToBody()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = sEntMan.System<BodySystem>();
        var containerSystem = sEntMan.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = sEntMan.SpawnEntity("MobHuman", mapData.GridCoords);
            ReplaceArmWithCyberArm(sEntMan, bodySystem, containerSystem, user, mapData.GridCoords, "OrganCyberArmLeftMilitaryT1");

            var militaryArm = bodySystem.GetAllOrgans(user).First(o => sEntMan.HasComponent<MilitaryCyberLimbComponent>(o));
            Assert.That(militaryArm, Is.Not.EqualTo(EntityUid.Invalid));
        });

        await pair.CleanReturnAsync();
    }
}
