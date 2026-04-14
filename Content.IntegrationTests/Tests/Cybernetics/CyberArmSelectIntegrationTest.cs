using System.Linq;
using Content.IntegrationTests;
using Content.Server.Cybernetics.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.Cybernetics.UI;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(CyberArmSelectSystem))]
public sealed class CyberArmSelectIntegrationTest
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
    public async Task EmptyHandActivate_OpensCyberArmSelectUI_WhenCyberArmHasItems()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true, DummyTicker = false });
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var userInterface = entityManager.System<UserInterfaceSystem>();
        var playerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var mapData = await pair.CreateTestMap();

        await pair.RunTicksSync(5);
        await PoolManager.WaitUntil(server, () => playerMan.Sessions.First().AttachedEntity != null);

        EntityUid cyberArm = default;
        EntityUid user = default;
        EntityUid screwdriver = default;

        await server.WaitAssertion(() =>
        {
            var session = playerMan.Sessions.First();
            var mindSystem = entityManager.System<SharedMindSystem>();
            mindSystem.WipeMind(session.ContentData()?.Mind);
            user = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            playerMan.SetAttachedEntity(session, user);
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, user, mapData.GridCoords);
            cyberArm = bodySystem.GetAllOrgans(user).First(o =>
                entityManager.HasComponent<CyberLimbComponent>(o));

            screwdriver = entityManager.SpawnEntity("Screwdriver", mapData.GridCoords);
            storageSystem.Insert(cyberArm, screwdriver, out _, user: null, playSound: false);
        });

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            var handsSystem = entityManager.System<SharedHandsSystem>();
            Assert.That(handsSystem.TryGetHand((user, entityManager.GetComponent<HandsComponent>(user)), "left", out _),
                Is.True, "Left hand (from cyber arm) should exist");
            if (handsSystem.TryGetHand((user, entityManager.GetComponent<HandsComponent>(user)), "left", out _))
                handsSystem.TrySetActiveHand((user, entityManager.GetComponent<HandsComponent>(user)), "left");

            var handled = handsSystem.TryUseItemInHand(user, altInteract: true, handName: "left");
            Assert.That(handled, Is.True, "TryUseItemInHand (alt) should open BUI");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, CyberArmSelectUiKey.Key, user), Is.True,
                "Cyber arm select UI should be open");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyberArmSelect_SpawnsVirtualItemWithUnremoveable_WhenItemSelected()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true, DummyTicker = false });
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitIdleAsync();
        await client.WaitIdleAsync();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();
        var bodySystem = sEntMan.System<BodySystem>();
        var containerSystem = sEntMan.System<SharedContainerSystem>();
        var storageSystem = sEntMan.System<SharedStorageSystem>();
        var userInterface = sEntMan.System<UserInterfaceSystem>();
        var handsSystem = sEntMan.System<SharedHandsSystem>();
        var playerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var mapData = await pair.CreateTestMap();

        await pair.RunTicksSync(5);
        await PoolManager.WaitUntil(server, () => playerMan.Sessions.First().AttachedEntity != null);

        EntityUid cyberArm = default;
        EntityUid user = default;
        EntityUid screwdriver = default;
        NetEntity userNet = default;
        NetEntity cyberArmNet = default;
        NetEntity screwdriverNet = default;

        await server.WaitAssertion(() =>
        {
            var session = playerMan.Sessions.First();
            var mindSystem = sEntMan.System<SharedMindSystem>();
            mindSystem.WipeMind(session.ContentData()?.Mind);
            user = sEntMan.SpawnEntity("MobHuman", mapData.GridCoords);
            playerMan.SetAttachedEntity(session, user);
            userNet = sEntMan.GetNetEntity(user);
            ReplaceArmWithCyberArm(sEntMan, bodySystem, containerSystem, user, mapData.GridCoords);
            cyberArm = bodySystem.GetAllOrgans(user).First(o =>
                sEntMan.HasComponent<CyberLimbComponent>(o));
            cyberArmNet = sEntMan.GetNetEntity(cyberArm);

            screwdriver = sEntMan.SpawnEntity("Screwdriver", mapData.GridCoords);
            screwdriverNet = sEntMan.GetNetEntity(screwdriver);
            storageSystem.Insert(cyberArm, screwdriver, out _, user: null, playSound: false);
        });

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            handsSystem.TrySetActiveHand((user, sEntMan.GetComponent<HandsComponent>(user)), "left");
            Assert.That(handsSystem.TryUseItemInHand(user, altInteract: true, handName: "left"), Is.True,
                "TryUseItemInHand (alt) should open BUI");
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, CyberArmSelectUiKey.Key, user), Is.True,
                "Cyber arm select UI should be open");

            var msg = new CyberArmSelectRequestMessage(screwdriverNet);
            msg.Actor = user;
            userInterface.RaiseUiMessage(cyberArm, CyberArmSelectUiKey.Key, msg);
        });

        await pair.RunTicksSync(15);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, CyberArmSelectUiKey.Key, user), Is.False,
                "Cyber arm select UI should close after selection");

            Assert.That(handsSystem.TryGetActiveItem(user, out var held), Is.True,
                "User should have an item in hand");
            Assert.That(sEntMan.HasComponent<VirtualItemComponent>(held), Is.True,
                "Held item should be a virtual item");
            Assert.That(sEntMan.HasComponent<UnremoveableComponent>(held), Is.True,
                "Virtual item should have UnremoveableComponent");

            var virt = sEntMan.GetComponent<VirtualItemComponent>(held!.Value);
            Assert.That(virt.BlockingEntity, Is.EqualTo(screwdriver),
                "Virtual item should point to the screwdriver in storage");

            Assert.That(handsSystem.CanDrop(user, held.Value), Is.False,
                "Virtual item should not be droppable");
        });

        await pair.CleanReturnAsync();
    }
}
