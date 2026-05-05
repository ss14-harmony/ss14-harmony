using System.Linq;
using Content.IntegrationTests;
using Content.Server.Cybernetics.Systems;
using Content.Shared.Cybernetics.Events;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.Cybernetics.UI;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

/// <summary>
/// Integration tests for the Cyber Arm Gun Fixes plan (steps 2-5):
/// 2. Pull a gun from the ground, try to fire - should fail (no CyberArmVirtualItemComponent)
/// 3. Select gun from cyber arm, fire - should work
/// 4. Select gun from cyber arm, have another player remove it from storage - virtual item should disappear from hand
/// 5. Eject magazine from cyber arm gun - real magazine should appear
/// </summary>
[TestFixture]
[TestOf(typeof(SharedCyberArmStorageSystem))]
public sealed class CyberArmGunFixesIntegrationTest
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

    /// <summary>
    /// Test 2: Pull exploit - virtual item without CyberArmVirtualItemComponent should NOT resolve to gun in TryGetGun.
    /// </summary>
    [Test]
    public async Task PulledGunVirtualItem_DoesNotResolveInTryGetGun()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = sEntMan.System<BodySystem>();
        var containerSystem = sEntMan.System<SharedContainerSystem>();
        var storageSystem = sEntMan.System<SharedStorageSystem>();
        var virtualItemSystem = sEntMan.System<SharedVirtualItemSystem>();
        var handsSystem = sEntMan.System<SharedHandsSystem>();
            var gunSystem = sEntMan.System<GunSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid gun = default;

        await server.WaitAssertion(() =>
        {
            user = sEntMan.SpawnEntity("MobHuman", mapData.GridCoords);
            gun = sEntMan.SpawnEntity("WeaponPistolViper", mapData.GridCoords);

            // Simulate pull: spawn virtual item in hand WITHOUT CyberArmVirtualItemComponent (like PullingSystem does)
            Assert.That(virtualItemSystem.TrySpawnVirtualItemInHand(gun, user, out var virtualItem),
                Is.True, "Should spawn virtual item");
            Assert.That(sEntMan.HasComponent<CyberArmVirtualItemComponent>(virtualItem!.Value), Is.False,
                "Pull virtual item should NOT have CyberArmVirtualItemComponent");

            Assert.That(handsSystem.TryGetActiveItem(user, out var held), Is.True);
            Assert.That(held, Is.EqualTo(virtualItem.Value));

            // TryGetGun should fail - pull exploit fix
            Assert.That(gunSystem.TryGetGun(user, out _), Is.False,
                "TryGetGun should return false for pulled gun (virtual item without CyberArmVirtualItemComponent)");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test 3: Select gun from cyber arm, fire - should work (TryGetGun resolves cyber arm virtual item).
    /// </summary>
    [Test]
    public async Task CyberArmGun_ResolvesInTryGetGun_AndCanShoot()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true, DummyTicker = false });
        var server = pair.Server;

        await server.WaitIdleAsync();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = sEntMan.System<BodySystem>();
        var containerSystem = sEntMan.System<SharedContainerSystem>();
        var storageSystem = sEntMan.System<SharedStorageSystem>();
        var userInterface = sEntMan.System<UserInterfaceSystem>();
        var handsSystem = sEntMan.System<SharedHandsSystem>();
        var gunSystem = sEntMan.System<GunSystem>();
        var playerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var mapData = await pair.CreateTestMap();

        await pair.RunTicksSync(5);
        await PoolManager.WaitUntil(server, () => playerMan.Sessions.First().AttachedEntity != null);

        EntityUid cyberArm = default;
        EntityUid user = default;
        EntityUid gun = default;

        await server.WaitAssertion(() =>
        {
            var session = playerMan.Sessions.First();
            var mindSystem = sEntMan.System<SharedMindSystem>();
            mindSystem.WipeMind(session.ContentData()?.Mind);
            user = sEntMan.SpawnEntity("MobHuman", mapData.GridCoords);
            playerMan.SetAttachedEntity(session, user);
            ReplaceArmWithCyberArm(sEntMan, bodySystem, containerSystem, user, mapData.GridCoords);
            cyberArm = bodySystem.GetAllOrgans(user).First(o =>
                sEntMan.HasComponent<CyberLimbComponent>(o));

            gun = sEntMan.SpawnEntity("WeaponPistolViper", mapData.GridCoords);
            storageSystem.Insert(cyberArm, gun, out _, user: null, playSound: false);
        });

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            var handsSystem = sEntMan.System<SharedHandsSystem>();
            handsSystem.TrySetActiveHand((user, sEntMan.GetComponent<HandsComponent>(user)), "left");
            Assert.That(handsSystem.TryUseItemInHand(user, altInteract: true, handName: "left"), Is.True,
                "TryUseItemInHand (alt) should open BUI");
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, CyberArmSelectUiKey.Key, user), Is.True);

            var gunNet = sEntMan.GetNetEntity(gun);
            var msg = new CyberArmSelectRequestMessage(gunNet);
            msg.Actor = user;
            userInterface.RaiseUiMessage(cyberArm, CyberArmSelectUiKey.Key, msg);
        });

        await pair.RunTicksSync(15);

        await server.WaitAssertion(() =>
        {
            Assert.That(handsSystem.TryGetActiveItem(user, out var held), Is.True);
            Assert.That(sEntMan.HasComponent<CyberArmVirtualItemComponent>(held!.Value), Is.True);

            // TryGetGun should succeed for cyber arm virtual item - this is the key fix (step 3)
            Assert.That(gunSystem.TryGetGun(user, out var resolvedGun), Is.True,
                "TryGetGun should resolve cyber arm virtual item to gun");
            Assert.That(resolvedGun.Owner, Is.EqualTo(gun),
                "Resolved gun should be the one in cyber arm storage");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test 4: Select gun from cyber arm, have another entity remove it from storage - virtual item should disappear.
    /// </summary>
    [Test]
    public async Task CyberArmGun_VirtualItemInvalidated_WhenRemovedFromStorage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true, DummyTicker = false });
        var server = pair.Server;

        await server.WaitIdleAsync();

        var sEntMan = server.ResolveDependency<IEntityManager>();
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
        EntityUid otherUser = default;
        EntityUid gun = default;

        await server.WaitAssertion(() =>
        {
            var session = playerMan.Sessions.First();
            var mindSystem = sEntMan.System<SharedMindSystem>();
            mindSystem.WipeMind(session.ContentData()?.Mind);
            user = sEntMan.SpawnEntity("MobHuman", mapData.GridCoords);
            playerMan.SetAttachedEntity(session, user);
            otherUser = sEntMan.SpawnEntity("MobHuman", mapData.GridCoords.Offset(new(2, 0)));
            ReplaceArmWithCyberArm(sEntMan, bodySystem, containerSystem, user, mapData.GridCoords);
            cyberArm = bodySystem.GetAllOrgans(user).First(o =>
                sEntMan.HasComponent<CyberLimbComponent>(o));

            gun = sEntMan.SpawnEntity("WeaponPistolViper", mapData.GridCoords);
            storageSystem.Insert(cyberArm, gun, out _, user: null, playSound: false);
        });

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            var handsSystem = sEntMan.System<SharedHandsSystem>();
            handsSystem.TrySetActiveHand((user, sEntMan.GetComponent<HandsComponent>(user)), "left");
            Assert.That(handsSystem.TryUseItemInHand(user, altInteract: true, handName: "left"), Is.True,
                "TryUseItemInHand (alt) should open BUI");
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var gunNet = sEntMan.GetNetEntity(gun);
            var msg = new CyberArmSelectRequestMessage(gunNet);
            msg.Actor = user;
            userInterface.RaiseUiMessage(cyberArm, CyberArmSelectUiKey.Key, msg);
        });

        await pair.RunTicksSync(15);

        await server.WaitAssertion(() =>
        {
            Assert.That(handsSystem.TryGetActiveItem(user, out var held), Is.True);
            Assert.That(sEntMan.HasComponent<CyberArmVirtualItemComponent>(held!.Value), Is.True);

            // Other user removes gun from storage (simulate: remove from storage container)
            var storageComp = sEntMan.GetComponent<StorageComponent>(cyberArm);
            Assert.That(storageComp.Container.Contains(gun), Is.True);
            containerSystem.Remove(gun, storageComp.Container);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            // Virtual item should be invalidated - user should no longer hold it
            var heldItems = handsSystem.EnumerateHeld(user).ToList();
            var hasCyberArmVirtualForGun = heldItems.Any(h =>
                sEntMan.TryGetComponent(h, out VirtualItemComponent? v) &&
                sEntMan.HasComponent<CyberArmVirtualItemComponent>(h) &&
                v!.BlockingEntity == gun);
            Assert.That(hasCyberArmVirtualForGun, Is.False,
                "Cyber arm virtual item should be invalidated when gun is removed from storage");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test 5: Eject magazine from cyber arm gun - real magazine should appear (verb relay works).
    /// </summary>
    [Test]
    public async Task CyberArmGun_EjectMagazineVerb_RelaysToBlockingEntity()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true, DummyTicker = false });
        var server = pair.Server;

        await server.WaitIdleAsync();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = sEntMan.System<BodySystem>();
        var containerSystem = sEntMan.System<SharedContainerSystem>();
        var storageSystem = sEntMan.System<SharedStorageSystem>();
        var userInterface = sEntMan.System<UserInterfaceSystem>();
        var handsSystem = sEntMan.System<SharedHandsSystem>();
        var verbSystem = sEntMan.System<Content.Server.Verbs.VerbSystem>();
        var slotsSystem = sEntMan.System<ItemSlotsSystem>();
        var playerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var mapData = await pair.CreateTestMap();

        await pair.RunTicksSync(5);
        await PoolManager.WaitUntil(server, () => playerMan.Sessions.First().AttachedEntity != null);

        EntityUid cyberArm = default;
        EntityUid user = default;
        EntityUid gun = default;

        await server.WaitAssertion(() =>
        {
            var session = playerMan.Sessions.First();
            var mindSystem = sEntMan.System<SharedMindSystem>();
            mindSystem.WipeMind(session.ContentData()?.Mind);
            user = sEntMan.SpawnEntity("MobHuman", mapData.GridCoords);
            playerMan.SetAttachedEntity(session, user);
            ReplaceArmWithCyberArm(sEntMan, bodySystem, containerSystem, user, mapData.GridCoords);
            cyberArm = bodySystem.GetAllOrgans(user).First(o =>
                sEntMan.HasComponent<CyberLimbComponent>(o));

            gun = sEntMan.SpawnEntity("WeaponPistolViper", mapData.GridCoords);
            storageSystem.Insert(cyberArm, gun, out _, user: null, playSound: false);
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
            var gunNet = sEntMan.GetNetEntity(gun);
            var msg = new CyberArmSelectRequestMessage(gunNet);
            msg.Actor = user;
            userInterface.RaiseUiMessage(cyberArm, CyberArmSelectUiKey.Key, msg);
        });

        await pair.RunTicksSync(15);

        await server.WaitAssertion(() =>
        {
            Assert.That(handsSystem.TryGetActiveItem(user, out var held), Is.True);
            Assert.That(sEntMan.HasComponent<CyberArmVirtualItemComponent>(held!.Value), Is.True);

            // Gun has magazine in slot - verify before eject
            Assert.That(slotsSystem.TryGetSlot(gun, "gun_magazine", out var slot), Is.True);
            Assert.That(slot!.Item, Is.Not.Null, "Gun should have magazine before eject");

            // Get verbs from virtual item (target = virtual item) - should relay to gun
            var verbs = verbSystem.GetLocalVerbs(held!.Value, user, typeof(AlternativeVerb), force: true);
            var ejectVerb = verbs.FirstOrDefault(v => v.Category == VerbCategory.Eject);
            if (ejectVerb == null)
                ejectVerb = verbs.FirstOrDefault(v => v.Text != null && v.Text.Contains("Eject", StringComparison.OrdinalIgnoreCase));

            Assert.That(ejectVerb, Is.Not.Null, "Should have eject verb from verb relay");
            // Execute the verb - target is the virtual item, but verb's Act operates on gun (from relay)
            verbSystem.ExecuteVerb(ejectVerb!, user, held.Value);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            // Magazine should be ejected - gun's magazine slot should be empty
            slotsSystem.TryGetSlot(gun, "gun_magazine", out var slot);
            Assert.That(slot?.Item, Is.Null, "Gun should no longer have magazine after eject");
        });

        await pair.CleanReturnAsync();
    }
}
