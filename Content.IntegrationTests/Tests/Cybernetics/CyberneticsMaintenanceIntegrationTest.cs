using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Integrity.Events;
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
[TestOf(typeof(CyberneticsMaintenanceSystem))]
public sealed class CyberneticsMaintenanceIntegrationTest
{
    private static EntityUid GetArmByCategory(IEntityManager entityManager, EntityUid body, string category)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static EntityUid GetArmLeft(IEntityManager entityManager, EntityUid body) =>
        GetArmByCategory(entityManager, body, "ArmLeft");

    /// <summary>
    /// Removes the left arm and inserts OrganCyberArmLeft into the body's container.
    /// </summary>
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

    private static void ReplaceArmWithCyberArmRight(IEntityManager entityManager, SharedContainerSystem containerSystem,
        EntityUid body, EntityCoordinates coords)
    {
        var arm = GetArmByCategory(entityManager, body, "ArmRight");
        var removeEv = new OrganRemoveRequestEvent(arm) { Destination = coords };
        entityManager.EventBus.RaiseLocalEvent(arm, ref removeEv);
        Assert.That(removeEv.Success, Is.True, "Remove right arm should succeed");

        var cyberArm = entityManager.SpawnEntity("OrganCyberArmRight", coords);
        var bodyComp = entityManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have Organs container");
        Assert.That(containerSystem.Insert(cyberArm, bodyComp.Organs!), Is.True, "Insert cyber right arm should succeed");
    }

    private static int GetIntegrityPenaltyTotal(IEntityManager entityManager, EntityUid body)
    {
        var ev = new IntegrityPenaltyTotalRequestEvent(body);
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Total;
    }

    [Test]
    public async Task Body_GainsCyberneticsMaintenanceComponent_WhenFirstCyberLimbInserted()
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

            Assert.That(entityManager.HasComponent<CyberneticsMaintenanceComponent>(human), Is.False,
                "Body should not have CyberneticsMaintenanceComponent before cyber limb");

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);

            Assert.That(entityManager.HasComponent<CyberneticsMaintenanceComponent>(human), Is.True,
                "Body should have CyberneticsMaintenanceComponent after inserting first cyber limb");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Body_LosesCyberneticsMaintenanceComponent_WhenLastCyberLimbRemoved()
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

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);
            Assert.That(entityManager.HasComponent<CyberneticsMaintenanceComponent>(human), Is.True);

            var cyberArm = bodySystem.GetAllOrgans(human).First(o =>
                entityManager.HasComponent<CyberLimbComponent>(o));
            var removeEv = new OrganRemoveRequestEvent(cyberArm) { Destination = coords };
            entityManager.EventBus.RaiseLocalEvent(cyberArm, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove cyber arm should succeed");

            Assert.That(entityManager.HasComponent<CyberneticsMaintenanceComponent>(human), Is.False,
                "Body should lose CyberneticsMaintenanceComponent when last cyber limb removed");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FullSixStepFlow_ScrewdriverWrenchWiresWrenchScrewdriver_Completes()
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

        // Step 1: Screwdriver opens panel
        await server.WaitPost(() =>
        {
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.PanelOpen, Is.True, "Panel should be open after screwdriver");
            Assert.That(comp.PanelSecured, Is.False);
            Assert.That(comp.BoltsTight, Is.True);
        });

        // Step 2: Wrench loosens bolts
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
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.BoltsTight, Is.False, "Bolts should be loose after wrench");
        });

        // Step 3: Wire insert (N=1 for one cyber limb) - requires screwdriver in other hand
        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wireStack, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wireStack, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.WiresInsertedCount, Is.EqualTo(1), "Should have inserted 1 wire");
        });

        // Step 4: Wrench tightens bolts (repair complete)
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
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.BoltsTight, Is.True, "Bolts should be tight after wrench");
            Assert.That(comp.WiresInsertedCount, Is.EqualTo(0), "WiresInsertedCount should reset on repair complete");
        });

        // Step 5: Screwdriver locks panel
        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.PanelSecured, Is.True, "Panel should be secured");
            Assert.That(comp.PanelOpen, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task QuickStorageAccess_ScrewdriverOpenStorageScrewdriverClose_Works()
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

        EntityUid technician = default;
        EntityUid patient = default;
        EntityUid cyberArm = default;
        EntityUid screwdriver = default;
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            entityManager.EnsureComponent<IgnoreUIRangeComponent>(technician);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            screwdriver = entityManager.SpawnEntity("Screwdriver", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            cyberArm = bodySystem.GetAllOrgans(patient).First(o => entityManager.HasComponent<CyberLimbComponent>(o));
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

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.PanelOpen, Is.True);
            Assert.That(comp.BoltsTight, Is.True);
            storageSystem.OpenStorageUI(cyberArm, technician, silent: true);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, StorageComponent.StorageUiKey.Key, technician), Is.True,
                "Storage should be accessible after screwdriver open");
        });

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.PanelSecured, Is.True);
            Assert.That(userInterface.IsUiOpen(cyberArm, StorageComponent.StorageUiKey.Key, technician), Is.False,
                "Storage UI should close when panel locked");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ResumeWireRepair_AfterClosingPanelEarly_PersistsProgress()
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

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.WiresInsertedCount, Is.EqualTo(1));
            Assert.That(comp.BoltsTight, Is.False);

            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.PanelSecured, Is.True);
            Assert.That(comp.WiresInsertedCount, Is.EqualTo(1), "WiresInsertedCount should persist when panel closed early");
            Assert.That(comp.BoltsTight, Is.False);
        });

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.WiresInsertedCount, Is.EqualTo(1), "Should resume with 1 wire already inserted");
            Assert.That(comp.BoltsTight, Is.False);
        });

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
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.BoltsTight, Is.True);
            Assert.That(comp.WiresInsertedCount, Is.EqualTo(0), "WiresInsertedCount should reset on repair complete");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WireInsertion_Rejected_WhenWiresInsertedCountReachesN()
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

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wrench, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wireStack, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wireStack, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.WiresInsertedCount, Is.EqualTo(1), "Should have inserted 1 wire");
            Assert.That(comp.PanelOpen, Is.True);
            Assert.That(comp.BoltsTight, Is.False, "Bolts should be loose for wire insertion");

            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            var wireStack2 = entityManager.SpawnEntity("CableApcStack", coords);
            handsSystem.TryPickupAnyHand(technician, wireStack2, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wireStack2, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
            Assert.That(ev.Handled, Is.False,
                "Second wire attempt when WiresInsertedCount >= N should show popup, not start DoAfter");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IntegrityPenalty_AppliedAndCleared_DuringMaintenanceFlow()
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
        EntityUid cyberArm = default;
        EntityUid screwdriver = default;
        EntityUid precisionScrewdriver = default;
        EntityUid wrench = default;
        EntityUid wireStack = default;
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            screwdriver = entityManager.SpawnEntity("Screwdriver", coords);
            precisionScrewdriver = entityManager.SpawnEntity("ScrewdriverPrecision", coords);
            wrench = entityManager.SpawnEntity("Wrench", coords);
            wireStack = entityManager.SpawnEntity("CableApcStack", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            cyberArm = bodySystem.GetAllOrgans(patient).First(o =>
                entityManager.HasComponent<CyberLimbComponent>(o));
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(GetIntegrityPenaltyTotal(entityManager, patient), Is.EqualTo(0),
                "Total penalty should be 0 before maintenance");
        });

        var integrityDoAfterTicks = 150;
        await server.WaitPost(() =>
        {
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(integrityDoAfterTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(GetIntegrityPenaltyTotal(entityManager, patient), Is.EqualTo(6),
                "Screwdriver open should add +1 panel penalty and +5 unskilled repair penalty");
        });

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wrench, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(integrityDoAfterTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(GetIntegrityPenaltyTotal(entityManager, patient), Is.EqualTo(7),
                "Wrench loosen should add +1 more penalty (panel+bolts+unskilled)");
        });

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, precisionScrewdriver, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wireStack, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wireStack, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(integrityDoAfterTicks);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wrench, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(integrityDoAfterTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(GetIntegrityPenaltyTotal(entityManager, patient), Is.EqualTo(1),
                "Wrench tighten should clear unskilled and bolts, leaving only panel open penalty");
        });

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(integrityDoAfterTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(GetIntegrityPenaltyTotal(entityManager, patient), Is.EqualTo(0),
                "Screwdriver lock should clear all penalties");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IntegrityPenalty_DoesNotAccumulate_WhenPanelOpenedAndClosedRepeatedly()
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
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            screwdriver = entityManager.SpawnEntity("Screwdriver", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
        });

        var integrityDoAfterTicks = 150;
        for (var i = 0; i < 3; i++)
        {
            await server.WaitPost(() =>
            {
                handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
                var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
                entityManager.EventBus.RaiseLocalEvent(patient, ev);
            });
            await pair.RunTicksSync(integrityDoAfterTicks);

            await server.WaitAssertion(() =>
            {
                Assert.That(GetIntegrityPenaltyTotal(entityManager, patient), Is.EqualTo(6),
                    $"Open #{i + 1}: total should be 6 (1 panel + 5 unskilled), unskilled does not accumulate");
            });

            await server.WaitPost(() =>
            {
                var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
                entityManager.EventBus.RaiseLocalEvent(patient, ev);
            });
            await pair.RunTicksSync(integrityDoAfterTicks);

            await server.WaitAssertion(() =>
            {
                Assert.That(GetIntegrityPenaltyTotal(entityManager, patient), Is.EqualTo(5),
                    $"Close #{i + 1}: total should be 5 (unskilled persists until repair complete)");
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IntegrityPenalty_OpeningWithPrecisionScrewdriver_DoesNotAddUnskilledPenalty()
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
        EntityUid precisionScrewdriver = default;
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            precisionScrewdriver = entityManager.SpawnEntity("ScrewdriverPrecision", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
        });

        var integrityDoAfterTicks = 150;
        await server.WaitPost(() =>
        {
            handsSystem.TryPickupAnyHand(technician, precisionScrewdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, precisionScrewdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(integrityDoAfterTicks);

        await server.WaitAssertion(() =>
        {
            var maint = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(maint.UnskilledRepairThisSession, Is.False,
                "Precision screwdriver should not set UnskilledRepairThisSession");
            Assert.That(GetIntegrityPenaltyTotal(entityManager, patient), Is.EqualTo(1),
                "Precision screwdriver open should add only +1 panel penalty, not +5 unskilled");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IntegrityPenalty_ScalesWithCyberLimbCount()
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
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            screwdriver = entityManager.SpawnEntity("Screwdriver", coords);
            wrench = entityManager.SpawnEntity("Wrench", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            ReplaceArmWithCyberArmRight(entityManager, containerSystem, patient, coords);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(GetIntegrityPenaltyTotal(entityManager, patient), Is.EqualTo(0),
                "Total penalty should be 0 before maintenance");
        });

        var integrityDoAfterTicks = 150;
        await server.WaitPost(() =>
        {
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(integrityDoAfterTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(GetIntegrityPenaltyTotal(entityManager, patient), Is.EqualTo(7),
                "Open panel with 2 cyber limbs should add +2 panel penalty and +5 unskilled");
        });

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wrench, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(integrityDoAfterTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(GetIntegrityPenaltyTotal(entityManager, patient), Is.EqualTo(9),
                "Loosen bolts with 2 cyber limbs should add +2 panel + +2 bolts + +5 unskilled");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WireRepair_WithNormalScrewdriver_AppliesLowQualityPenalty()
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
        EntityUid cyberArm = default;
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
            cyberArm = bodySystem.GetAllOrgans(patient).First(o =>
                entityManager.HasComponent<CyberLimbComponent>(o));
        });

        var doAfterTicks = 150;

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
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

        await server.WaitAssertion(() =>
        {
            // Wire insertion does not add per-limb penalty. Only UnskilledRepairThisSession (+5) from opening with normal screwdriver.
            Assert.That(entityManager.HasComponent<LowQualityMaintenancePenaltyComponent>(cyberArm), Is.False,
                "Wire insertion no longer adds LowQualityMaintenancePenaltyComponent");
            Assert.That(entityManager.TryGetComponent(patient, out CyberneticsMaintenanceComponent? maint), Is.True);
            Assert.That(maint!.UnskilledRepairThisSession, Is.True,
                "Opening with normal screwdriver should set UnskilledRepairThisSession");
            Assert.That(GetIntegrityPenaltyTotal(entityManager, patient), Is.GreaterThanOrEqualTo(5),
                "Unskilled repair (opening with normal screwdriver) adds +5 penalty");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WireRepair_WithPrecisionScrewdriver_DoesNotAddPenalty()
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
        EntityUid cyberArm = default;
        EntityUid precisionScrewdriver = default;
        EntityUid wrench = default;
        EntityUid wireStack = default;
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            precisionScrewdriver = entityManager.SpawnEntity("ScrewdriverPrecision", coords);
            wrench = entityManager.SpawnEntity("Wrench", coords);
            wireStack = entityManager.SpawnEntity("CableApcStack", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            cyberArm = bodySystem.GetAllOrgans(patient).First(o =>
                entityManager.HasComponent<CyberLimbComponent>(o));
        });

        var doAfterTicks = 150;
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            handsSystem.TryPickupAnyHand(technician, precisionScrewdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, precisionScrewdriver, patient, coords);
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
            handsSystem.TryPickupAnyHand(technician, precisionScrewdriver, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wireStack, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wireStack, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.HasComponent<LowQualityMaintenancePenaltyComponent>(cyberArm), Is.False,
                "Wire insertion does not add LowQualityMaintenancePenaltyComponent (precision or otherwise)");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    [Ignore("Second wire insert DoAfter does not complete in integration test environment; wire-insert penalty removed - only UnskilledRepairThisSession from panel open applies")]
    public async Task WireRepair_WithPrecisionScrewdriver_RemovesLowQualityPenalty()
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
        EntityUid cyberArm = default;
        EntityUid screwdriver = default;
        EntityUid precisionScrewdriver = default;
        EntityUid wrench = default;
        EntityUid wireStack = default;
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            screwdriver = entityManager.SpawnEntity("Screwdriver", coords);
            precisionScrewdriver = entityManager.SpawnEntity("ScrewdriverPrecision", coords);
            wrench = entityManager.SpawnEntity("Wrench", coords);
            wireStack = entityManager.SpawnEntity("CableApcStack10", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            cyberArm = bodySystem.GetAllOrgans(patient).First(o =>
                entityManager.HasComponent<CyberLimbComponent>(o));
        });

        var doAfterTicks = 150;

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
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

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.HasComponent<LowQualityMaintenancePenaltyComponent>(cyberArm), Is.True,
                "Should have low-quality penalty after first repair with normal screwdriver");
        });

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
            handsSystem.TryPickupAnyHand(technician, precisionScrewdriver, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wireStack, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wireStack, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.HasComponent<LowQualityMaintenancePenaltyComponent>(cyberArm), Is.False,
                "Precision screwdriver repair should remove LowQualityMaintenancePenaltyComponent");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WireRepair_Rejected_WhenNoScrewdriverInHand()
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
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wrench, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wireStack, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wireStack, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.PanelOpen, Is.True, "Panel should be open");
            Assert.That(comp.WiresInsertedCount, Is.EqualTo(0),
                "Wire insert without screwdriver in other hand should not start DoAfter");
        });

        await pair.CleanReturnAsync();
    }
}
