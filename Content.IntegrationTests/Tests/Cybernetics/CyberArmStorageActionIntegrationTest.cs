using System.Linq;
using Content.IntegrationTests;
using Content.Server.Cybernetics.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.Cybernetics.UI;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(CyberArmSelectSystem))]
public sealed class CyberArmStorageActionIntegrationTest
{
    private static EntityUid GetArmByCategory(IEntityManager entityManager, EntityUid body, string category)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static EntityUid ReplaceArmWithCyberArm(
        IEntityManager entityManager,
        SharedContainerSystem containerSystem,
        EntityUid body,
        EntityCoordinates coords,
        string category,
        string cyberArmProto)
    {
        var arm = GetArmByCategory(entityManager, body, category);
        var removeEv = new OrganRemoveRequestEvent(arm) { Destination = coords };
        entityManager.EventBus.RaiseLocalEvent(arm, ref removeEv);
        Assert.That(removeEv.Success, Is.True, $"Removing {category} should succeed");

        var cyberArm = entityManager.SpawnEntity(cyberArmProto, coords);
        var bodyComp = entityManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have organs container");
        Assert.That(containerSystem.Insert(cyberArm, bodyComp.Organs!), Is.True, "Insert cyber arm should succeed");
        return cyberArm;
    }

    private static EntityUid GetActionForContainer(IEntityManager entityManager, EntityUid user, EntityUid container)
    {
        var actionsComp = entityManager.GetComponent<ActionsComponent>(user);
        var query = entityManager.GetEntityQuery<ActionComponent>();
        var instantQuery = entityManager.GetEntityQuery<InstantActionComponent>();
        var matching = actionsComp.Actions
            .Where(action => query.TryComp(action, out var comp)
                             && comp.Container == container
                             && instantQuery.TryComp(action, out var instant)
                             && instant.Event is OpenCyberArmStorageActionEvent)
            .ToList();

        Assert.That(matching.Count, Is.EqualTo(1), "Expected exactly one cyber arm storage action for container");
        return matching[0];
    }

    [Test]
    public async Task Action_GrantedRemovedReinserted_And_AltUseMatchesPerArmBehavior()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true, DummyTicker = false });
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var userInterface = entityManager.System<UserInterfaceSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var actionsSystem = entityManager.System<SharedActionsSystem>();
        var playerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var mapData = await pair.CreateTestMap();

        await pair.RunTicksSync(5);
        await PoolManager.WaitUntil(server, () => playerMan.Sessions.First().AttachedEntity != null);

        EntityUid user = default;
        EntityUid leftCyberArm = default;
        EntityUid rightCyberArm = default;
        EntityUid leftStoredItem = default;

        await server.WaitAssertion(() =>
        {
            var session = playerMan.Sessions.First();
            var mindSystem = entityManager.System<SharedMindSystem>();
            mindSystem.WipeMind(session.ContentData()?.Mind);

            user = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            playerMan.SetAttachedEntity(session, user);

            leftCyberArm = ReplaceArmWithCyberArm(entityManager, containerSystem, user, mapData.GridCoords, "ArmLeft", "OrganCyberArmLeft");
            rightCyberArm = ReplaceArmWithCyberArm(entityManager, containerSystem, user, mapData.GridCoords, "ArmRight", "OrganCyberArmRight");

            var leftItem = entityManager.SpawnEntity("Screwdriver", mapData.GridCoords);
            var rightItem = entityManager.SpawnEntity("Wrench", mapData.GridCoords);
            storageSystem.Insert(leftCyberArm, leftItem, out _, user: null, playSound: false);
            storageSystem.Insert(rightCyberArm, rightItem, out _, user: null, playSound: false);
            leftStoredItem = leftItem;
        });

        await pair.RunTicksSync(5);

        EntityUid leftAction = default;
        EntityUid rightAction = default;
        await server.WaitAssertion(() =>
        {
            leftAction = GetActionForContainer(entityManager, user, leftCyberArm);
            rightAction = GetActionForContainer(entityManager, user, rightCyberArm);

            var leftActionComp = entityManager.GetComponent<ActionComponent>(leftAction);
            var rightActionComp = entityManager.GetComponent<ActionComponent>(rightAction);
            Assert.That(leftActionComp.Container, Is.EqualTo(leftCyberArm));
            Assert.That(rightActionComp.Container, Is.EqualTo(rightCyberArm));
        });

        await server.WaitAssertion(() =>
        {
            var handsComp = entityManager.GetComponent<HandsComponent>(user);
            handsSystem.TrySetActiveHand((user, handsComp), "right");

            var actionsComp = entityManager.GetComponent<ActionsComponent>(user);
            actionsSystem.PerformAction((user, actionsComp), (leftAction, entityManager.GetComponent<ActionComponent>(leftAction)));
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var handsComp = entityManager.GetComponent<HandsComponent>(user);
            Assert.That(handsComp.ActiveHandId, Is.EqualTo("left"),
                "Left arm action should auto-switch active hand to left");

            Assert.That(userInterface.IsUiOpen(leftCyberArm, CyberArmSelectUiKey.Key, user), Is.True,
                "Left arm UI should open when left action is used");
            Assert.That(userInterface.IsUiOpen(rightCyberArm, CyberArmSelectUiKey.Key, user), Is.False,
                "Right arm UI should not open when left action is used");
        });

        await server.WaitAssertion(() =>
        {
            var blockingItem = entityManager.SpawnEntity("Crowbar", mapData.GridCoords);
            var handsComp = entityManager.GetComponent<HandsComponent>(user);
            handsSystem.TrySetActiveHand((user, handsComp), "left");
            Assert.That(handsSystem.TryPickup(user, blockingItem, "left", checkActionBlocker: false, animate: false), Is.True,
                "Precondition: left hand should be occupied");

            var msg = new CyberArmSelectRequestMessage(entityManager.GetNetEntity(leftStoredItem))
            {
                Actor = user
            };
            userInterface.RaiseUiMessage(leftCyberArm, CyberArmSelectUiKey.Key, msg);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var handsComp = entityManager.GetComponent<HandsComponent>(user);
            Assert.That(handsSystem.TryGetHeldItem((user, handsComp), "left", out var leftHeld), Is.True,
                "Left hand should still be occupied after selecting with a full hand");
            Assert.That(leftHeld, Is.Not.Null);
            Assert.That(entityManager.HasComponent<VirtualItemComponent>(leftHeld.Value), Is.False,
                "Selecting cyber arm storage with a full hand should not replace held item with a virtual item");
        });

        await server.WaitAssertion(() =>
        {
            var handsComp = entityManager.GetComponent<HandsComponent>(user);
            handsSystem.TrySetActiveHand((user, handsComp), "right");
            var handled = handsSystem.TryUseItemInHand(user, altInteract: true, handName: "right");
            Assert.That(handled, Is.True, "Alt-use on right cyber hand should be handled");
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(rightCyberArm, CyberArmSelectUiKey.Key, user), Is.True,
                "Alt-use on right hand should open right arm UI");
            Assert.That(userInterface.IsUiOpen(leftCyberArm, CyberArmSelectUiKey.Key, user), Is.False,
                "Opening right arm UI should close left arm UI");
        });

        await server.WaitAssertion(() =>
        {
            var removeEv = new OrganRemoveRequestEvent(leftCyberArm) { Destination = mapData.GridCoords };
            entityManager.EventBus.RaiseLocalEvent(leftCyberArm, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Removing left cyber arm should succeed");
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var actionsComp = entityManager.GetComponent<ActionsComponent>(user);
            var actionQuery = entityManager.GetEntityQuery<ActionComponent>();
            Assert.That(actionsComp.Actions.Any(action => actionQuery.TryComp(action, out var comp) && comp.Container == leftCyberArm), Is.False,
                "Left arm action should be removed when left arm is detached");
            Assert.That(actionsComp.Actions.Any(action => actionQuery.TryComp(action, out var comp) && comp.Container == rightCyberArm), Is.True,
                "Right arm action should remain when only left arm is detached");
        });

        await server.WaitAssertion(() =>
        {
            var bodyComp = entityManager.GetComponent<BodyComponent>(user);
            Assert.That(containerSystem.Insert(leftCyberArm, bodyComp.Organs!), Is.True, "Reinserting left cyber arm should succeed");
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            _ = GetActionForContainer(entityManager, user, leftCyberArm);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AltUse_BiologicalHand_DoesNotOpenCyberArmSelect()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true, DummyTicker = false });
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var userInterface = entityManager.System<UserInterfaceSystem>();
        var playerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var mapData = await pair.CreateTestMap();

        await pair.RunTicksSync(5);
        await PoolManager.WaitUntil(server, () => playerMan.Sessions.First().AttachedEntity != null);

        EntityUid user = default;
        EntityUid leftCyberArm = default;

        await server.WaitAssertion(() =>
        {
            var session = playerMan.Sessions.First();
            var mindSystem = entityManager.System<SharedMindSystem>();
            mindSystem.WipeMind(session.ContentData()?.Mind);

            user = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            playerMan.SetAttachedEntity(session, user);
            leftCyberArm = ReplaceArmWithCyberArm(entityManager, containerSystem, user, mapData.GridCoords, "ArmLeft", "OrganCyberArmLeft");
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var handsComp = entityManager.GetComponent<HandsComponent>(user);
            handsSystem.TrySetActiveHand((user, handsComp), "right");
            var handled = handsSystem.TryUseItemInHand(user, altInteract: true, handName: "right");
            Assert.That(handled, Is.False, "Alt-use on biological hand should not trigger cyber arm select");
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(leftCyberArm, CyberArmSelectUiKey.Key, user), Is.False,
                "Cyber arm select UI should remain closed when alt-using biological hand");
        });

        await pair.CleanReturnAsync();
    }
}
