using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

/// <summary>
/// Integration tests for cyber limb variants: storage slot counts, military damage protection.
/// </summary>
[TestFixture]
[TestOf(typeof(CyberLimbDamageProtectionSystem))]
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
    public async Task MilitaryCyberArm_AddsDamageProtection_WhenAttached()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = sEntMan.System<BodySystem>();
        var containerSystem = sEntMan.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid user = default;

        await server.WaitAssertion(() =>
        {
            user = sEntMan.SpawnEntity("MobHuman", mapData.GridCoords);
            ReplaceArmWithCyberArm(sEntMan, bodySystem, containerSystem, user, mapData.GridCoords, "OrganCyberArmLeftMilitaryT1");

            Assert.That(sEntMan.TryGetComponent(user, out DamageProtectionBuffComponent? buff), Is.True,
                "Body should have DamageProtectionBuffComponent when military limb attached");
            Assert.That(buff!.Modifiers.ContainsKey("MilitaryCyberlimb"), Is.True,
                "DamageProtectionBuffComponent should have MilitaryCyberlimb modifier");
            Assert.That(buff.Modifiers["MilitaryCyberlimb"].ID, Is.EqualTo("MilitaryCyberlimb5Percent"),
                "Modifier should be MilitaryCyberlimb5Percent");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MilitaryCyberArm_RemovesDamageProtection_WhenDetached()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = sEntMan.System<BodySystem>();
        var containerSystem = sEntMan.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid militaryArm = default;

        await server.WaitAssertion(() =>
        {
            user = sEntMan.SpawnEntity("MobHuman", mapData.GridCoords);
            ReplaceArmWithCyberArm(sEntMan, bodySystem, containerSystem, user, mapData.GridCoords, "OrganCyberArmLeftMilitaryT1");

            militaryArm = bodySystem.GetAllOrgans(user).First(o => sEntMan.HasComponent<MilitaryCyberLimbComponent>(o));
            Assert.That(sEntMan.HasComponent<DamageProtectionBuffComponent>(user), Is.True,
                "Body should have DamageProtectionBuffComponent before detach");

            var removeEv = new OrganRemoveRequestEvent(militaryArm) { Destination = mapData.GridCoords };
            sEntMan.EventBus.RaiseLocalEvent(militaryArm, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove military arm should succeed");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.HasComponent<DamageProtectionBuffComponent>(user), Is.False,
                "Body should not have DamageProtectionBuffComponent when military limb detached");
        });

        await pair.CleanReturnAsync();
    }
}
