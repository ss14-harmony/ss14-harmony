using System.Linq;
using Content.IntegrationTests;
using Content.Server.Cybernetics.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(CyberLimbStorageSystem))]
public sealed class CyberLimbStorageIntegrationTest
{
    private static EntityUid GetArmLeft(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("ArmLeft") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static void ReplaceArmWithCyberArm(IEntityManager entityManager, BodySystem bodySystem,
        SharedContainerSystem containerSystem, EntityUid body, EntityCoordinates coords)
    {
        var arm = GetArmLeft(entityManager, body);
        var removeEv = new OrganRemoveRequestEvent(arm) { Destination = coords };
        entityManager.EventBus.RaiseLocalEvent(arm, ref removeEv);
        Assert.That(removeEv.Success, Is.True, "Remove arm should succeed");

        var cyberArm = entityManager.SpawnEntity("OrganCyberArmLeft", coords);
        var bodyComp = entityManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have Organs container");
        Assert.That(containerSystem.Insert(cyberArm, bodyComp.Organs!), Is.True, "Insert cyber arm should succeed");
    }

    [Test]
    public async Task Storage_Accessible_WhenDetached()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var userInterface = entityManager.System<UserInterfaceSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid cyberArm = default;
        EntityUid user = default;

        await server.WaitAssertion(() =>
        {
            user = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            cyberArm = entityManager.SpawnEntity("OrganCyberArmLeft", mapData.GridCoords);

            Assert.That(entityManager.HasComponent<StorageComponent>(cyberArm), Is.True, "Cyber arm should have storage");
            storageSystem.OpenStorageUI(cyberArm, user, silent: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, StorageComponent.StorageUiKey.Key, user), Is.True,
                "Storage UI should be open when limb is detached");

            var item = entityManager.SpawnEntity("Screwdriver", mapData.GridCoords);
            var storageComp = entityManager.GetComponent<StorageComponent>(cyberArm);
            var inserted = storageSystem.Insert(cyberArm, item, out _, user: user, playSound: false);
            Assert.That(inserted, Is.True, "Insert should succeed");
            Assert.That(storageComp.Container.ContainedEntities, Has.Count.EqualTo(1),
                "Storage should contain 1 item");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Storage_Blocked_WhenAttached()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var userInterface = entityManager.System<UserInterfaceSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid cyberArm = default;
        EntityUid user = default;

        await server.WaitAssertion(() =>
        {
            user = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, user, mapData.GridCoords);
            cyberArm = bodySystem.GetAllOrgans(user).First(o =>
                entityManager.HasComponent<CyberLimbComponent>(o));

            storageSystem.OpenStorageUI(cyberArm, user, silent: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, StorageComponent.StorageUiKey.Key, user), Is.False,
                "Storage UI should not open when limb is attached to body");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Storage_UI_Closes_WhenLimbAttached()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var userInterface = entityManager.System<UserInterfaceSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid cyberArm = default;
        EntityUid user = default;

        await server.WaitAssertion(() =>
        {
            user = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            cyberArm = entityManager.SpawnEntity("OrganCyberArmLeft", mapData.GridCoords);
            storageSystem.OpenStorageUI(cyberArm, user, silent: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, StorageComponent.StorageUiKey.Key, user), Is.True,
                "Storage UI should be open before attach");

            var arm = GetArmLeft(entityManager, user);
            var removeEv = new OrganRemoveRequestEvent(arm) { Destination = mapData.GridCoords };
            entityManager.EventBus.RaiseLocalEvent(arm, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove arm should succeed");

            var bodyComp = entityManager.GetComponent<BodyComponent>(user);
            Assert.That(containerSystem.Insert(cyberArm, bodyComp.Organs!), Is.True, "Insert cyber arm should succeed");

            Assert.That(userInterface.IsUiOpen(cyberArm, StorageComponent.StorageUiKey.Key, user), Is.False,
                "Storage UI should close when limb is attached");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonStacking_SplitsStackOnInsert()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var stackSystem = entityManager.System<Content.Server.Stack.StackSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid cyberArm = default;
        EntityUid wireStack = default;

        await server.WaitAssertion(() =>
        {
            cyberArm = entityManager.SpawnEntity("OrganCyberArmLeft", mapData.GridCoords);
            wireStack = entityManager.SpawnEntity("CableApcStack", mapData.GridCoords);
            stackSystem.SetCount((wireStack, null), 3);

            Assert.That(entityManager.GetComponent<StackComponent>(wireStack).Count, Is.EqualTo(3),
                "Stack should have 3 before insert");

            storageSystem.Insert(cyberArm, wireStack, out _, user: null, playSound: false, stackAutomatically: false);
        });

        await server.WaitAssertion(() =>
        {
            var storageComp = entityManager.GetComponent<StorageComponent>(cyberArm);
            var contained = storageComp.Container.ContainedEntities.ToList();
            Assert.That(contained, Has.Count.EqualTo(1), "Storage should contain exactly 1 item (non-stacking)");
            Assert.That(entityManager.GetComponent<StackComponent>(contained[0]).Count, Is.EqualTo(1),
                "Inserted item should have count 1");

            Assert.That(entityManager.GetComponent<StackComponent>(wireStack).Count, Is.EqualTo(2),
                "Original stack should retain 2 after 1 was split off");
        });

        await pair.CleanReturnAsync();
    }
}
