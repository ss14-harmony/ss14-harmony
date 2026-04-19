using System.Linq;
using Content.IntegrationTests;
using Content.Server.Cybernetics.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
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
[TestOf(typeof(CyberLimbStatsSystem))]
public sealed class CyberLimbStatsIntegrationTest
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
    public async Task StatsComponent_Added_WhenCyberLimbAttached()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;

            Assert.That(entityManager.HasComponent<CyberLimbStatsComponent>(human), Is.False,
                "Body should not have CyberLimbStatsComponent before cyber limb");

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);

            Assert.That(entityManager.HasComponent<CyberLimbStatsComponent>(human), Is.True,
                "Body should have CyberLimbStatsComponent after inserting cyber limb");

            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(human);
            Assert.That(stats.ServiceTimeMax, Is.GreaterThan(TimeSpan.Zero), "ServiceTimeMax should be positive");
            Assert.That(stats.ServiceTimeRemaining, Is.GreaterThan(TimeSpan.Zero), "ServiceTimeRemaining should be positive");
            Assert.That(stats.ServiceTimeRemaining, Is.EqualTo(stats.ServiceTimeMax), "ServiceTimeRemaining should equal ServiceTimeMax on fresh install");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ServiceTime_Drains_OverTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;
        TimeSpan initialRemaining = default;

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);
            patient = human;
            initialRemaining = entityManager.GetComponent<CyberLimbStatsComponent>(human).ServiceTimeRemaining;
        });

        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            Assert.That(stats.ServiceTimeRemaining, Is.LessThan(initialRemaining),
                "ServiceTimeRemaining should have decreased after ~2.5 seconds");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Repair_ResetsServiceTime_ToMax()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid technician = default;
        EntityUid patient = default;
        EntityUid screwdriver = default;
        EntityUid wrench = default;
        EntityUid wireStack = default;
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            screwdriver = entityManager.SpawnEntity("Screwdriver", coords);
            wrench = entityManager.SpawnEntity("Wrench", coords);
            wireStack = entityManager.SpawnEntity("CableApcStack", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
        });

        var doAfterTicks = 150;

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wrench, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wireStack, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wireStack, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wrench, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            Assert.That(stats.ServiceTimeRemaining, Is.GreaterThanOrEqualTo(stats.ServiceTimeMax - TimeSpan.FromSeconds(25)),
                "ServiceTimeRemaining should be reset to near ServiceTimeMax after wire repair (allow ~25s drain during test DoAfters)");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Efficiency_Drops_WhenDepleted()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);
            patient = human;

            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(human);
            stats.BaseServiceRemaining = TimeSpan.Zero;
            entityManager.Dirty(human, stats);
        });

        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            Assert.That(stats.Efficiency, Is.EqualTo(0.5f), "Efficiency should be 0.5 when depleted");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Storage_Accessible_WhenPanelOpen_Blocked_WhenClosed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var userInterface = entityManager.System<UserInterfaceSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;
        EntityUid cyberArm = default;
        EntityUid technician = default;
        EntityUid screwdriver = default;
        EntityUid wrench = default;
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            entityManager.EnsureComponent<IgnoreUIRangeComponent>(technician);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            screwdriver = entityManager.SpawnEntity("Screwdriver", coords);
            wrench = entityManager.SpawnEntity("Wrench", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            cyberArm = bodySystem.GetAllOrgans(patient).First(o => entityManager.HasComponent<CyberLimbComponent>(o));

            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.PanelOpen, Is.True, "Panel should be open after screwdriver");
            Assert.That(comp.BoltsTight, Is.True, "Bolts should be tight");
            storageSystem.OpenStorageUI(cyberArm, technician, silent: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, StorageComponent.StorageUiKey.Key, technician), Is.True,
                "Storage UI should be open when panel is open and bolts are tight");
        });

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wrench, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.BoltsTight, Is.False, "Bolts should be loose after wrench");
            storageSystem.OpenStorageUI(cyberArm, technician, silent: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, StorageComponent.StorageUiKey.Key, technician), Is.False,
                "Storage UI should not open when bolts are loose");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StorageUI_ForceClosed_WhenPanelClosed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var userInterface = entityManager.System<UserInterfaceSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;
        EntityUid cyberArm = default;
        EntityUid technician = default;
        EntityUid screwdriver = default;
        EntityUid wrench = default;
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            entityManager.EnsureComponent<IgnoreUIRangeComponent>(technician);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            screwdriver = entityManager.SpawnEntity("Screwdriver", coords);
            wrench = entityManager.SpawnEntity("Wrench", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            cyberArm = bodySystem.GetAllOrgans(patient).First(o => entityManager.HasComponent<CyberLimbComponent>(o));

            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.PanelOpen, Is.True, "Panel should be open after screwdriver");
            storageSystem.OpenStorageUI(cyberArm, technician, silent: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, StorageComponent.StorageUiKey.Key, technician), Is.True,
                "Storage UI should be open before panel close");
        });

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, StorageComponent.StorageUiKey.Key, technician), Is.False,
                "Storage UI should be force-closed when panel is closed");
        });

        await pair.CleanReturnAsync();
    }
}
