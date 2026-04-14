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
[TestOf(typeof(CyberLimbModuleSystem))]
public sealed class CyberLimbModuleSystemIntegrationTest
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
    public async Task BaseServiceTime_WithoutModules()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid human = default;
        await server.WaitAssertion(() =>
        {
            human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);

            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(human);
            Assert.That(stats.ServiceTimeMax, Is.EqualTo(TimeSpan.FromMinutes(5)), "1 limb with no modules = 5 min base");
            Assert.That(stats.ServiceTimeRemaining, Is.EqualTo(TimeSpan.FromMinutes(5)), "First install fills base");
        });

        await pair.RunTicksSync(60);

        await server.WaitAssertion(() =>
        {
            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(human);
            Assert.That(stats.ServiceTimeRemaining, Is.LessThan(TimeSpan.FromMinutes(5)),
                "Service time should drain after ~1 second");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ServiceTime_SummedFromMatterBins_WithBase()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;

            var arm = GetArmLeft(entityManager, human);
            var removeEv = new OrganRemoveRequestEvent(arm) { Destination = coords };
            entityManager.EventBus.RaiseLocalEvent(arm, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove arm should succeed");

            var cyberArm = entityManager.SpawnEntity("OrganCyberArmLeft", coords);
            var mb1 = entityManager.SpawnEntity("MatterBinStockPart", coords);
            var mb2 = entityManager.SpawnEntity("MatterBinStockPart", coords);
            storageSystem.Insert(cyberArm, mb1, out _, user: null, playSound: false);
            storageSystem.Insert(cyberArm, mb2, out _, user: null, playSound: false);

            var bodyComp = entityManager.GetComponent<BodyComponent>(human);
            Assert.That(containerSystem.Insert(cyberArm, bodyComp.Organs!), Is.True, "Insert cyber arm should succeed");

            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(human);
            Assert.That(stats.ServiceTimeMax, Is.EqualTo(TimeSpan.FromMinutes(25)).Within(TimeSpan.FromMilliseconds(100)), "5 base + 2*10 matter bins = 25 min");
            Assert.That(stats.ServiceTimeRemaining, Is.EqualTo(TimeSpan.FromMinutes(25)).Within(TimeSpan.FromMilliseconds(100)), "First install fills matter bins");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MatterBin_InsertedEmpty()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);

            var cyberArm = bodySystem.GetAllOrgans(human).First(o => entityManager.HasComponent<CyberLimbComponent>(o));
            entityManager.EnsureComponent<CyberneticsMaintenanceComponent>(human);
            var maint = entityManager.GetComponent<CyberneticsMaintenanceComponent>(human);
            maint.PanelOpen = true;
            maint.BoltsTight = true;
            entityManager.Dirty(human, maint);

            var mb = entityManager.SpawnEntity("MatterBinStockPart", coords);
            storageSystem.Insert(cyberArm, mb, out _, user: null, playSound: false);

            var matterBin = entityManager.GetComponent<CyberLimbMatterBinComponent>(mb);
            Assert.That(matterBin.ServiceRemaining, Is.EqualTo(TimeSpan.Zero), "Inserted matter bin should be empty");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FirstInstall_FillsMatterBins()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;

            var arm = GetArmLeft(entityManager, human);
            var removeEv = new OrganRemoveRequestEvent(arm) { Destination = coords };
            entityManager.EventBus.RaiseLocalEvent(arm, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove arm should succeed");

            var cyberArm = entityManager.SpawnEntity("OrganCyberArmLeft", coords);
            var mb = entityManager.SpawnEntity("MatterBinStockPart", coords);
            storageSystem.Insert(cyberArm, mb, out _, user: null, playSound: false);

            var bodyComp = entityManager.GetComponent<BodyComponent>(human);
            Assert.That(containerSystem.Insert(cyberArm, bodyComp.Organs!), Is.True, "Insert cyber arm should succeed");

            var matterBin = entityManager.GetComponent<CyberLimbMatterBinComponent>(mb);
            Assert.That(matterBin.ServiceRemaining, Is.EqualTo(matterBin.ServiceTime), "First install should fill matter bin");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Efficiency_FromCpus_ExternalMultiplier()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var moduleSystem = entityManager.System<CyberLimbModuleSystem>();
        var statsSystem = entityManager.System<CyberLimbStatsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);
            patient = human;

            var cyberArm = bodySystem.GetAllOrgans(human).First(o => entityManager.HasComponent<CyberLimbComponent>(o));
            entityManager.EnsureComponent<CyberneticsMaintenanceComponent>(human);
            var maint = entityManager.GetComponent<CyberneticsMaintenanceComponent>(human);
            maint.PanelOpen = true;
            maint.BoltsTight = true;
            entityManager.Dirty(human, maint);

            for (var i = 0; i < 6; i++)
            {
                var cpu = entityManager.SpawnEntity("TreasureCPUSupercharged", coords);
                storageSystem.Insert(cyberArm, cpu, out _, user: null, playSound: false);
            }

            var (_, cpuCount, _) = moduleSystem.GetModuleCounts(patient);
            Assert.That(cpuCount, Is.EqualTo(6), $"GetModuleCounts should return 6 CPUs, got {cpuCount}");
            statsSystem.RecomputeAndRefresh(patient);

            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            Assert.That(stats.Efficiency, Is.EqualTo(1.6f).Within(0.001f), "6 CPUs = 160% limb efficiency");

            var bodyStats = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            bodyStats.BaseServiceRemaining = TimeSpan.Zero;
            entityManager.Dirty(patient, bodyStats);

            var totalRemaining = moduleSystem.GetTotalServiceRemaining(patient);
            Assert.That(totalRemaining, Is.EqualTo(TimeSpan.Zero), $"GetTotalServiceRemaining should be 0, got {totalRemaining}");

            statsSystem.RecomputeAndRefresh(patient);

            var statsAfterDeplete = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            Assert.That(statsAfterDeplete.ServiceTimeRemaining, Is.EqualTo(TimeSpan.Zero), "ServiceTimeRemaining should be 0 when depleted");
            Assert.That(statsAfterDeplete.Efficiency, Is.EqualTo(0.8f).Within(0.001f), "160% * 50% depletion = 80%");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Repair_ResetsEfficiencyFromCpus()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;
        EntityUid technician = default;
        EntityUid screwdriver = default;
        EntityUid wrench = default;
        EntityUid wireStack = default;
        EntityUid cpu = default;
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            screwdriver = entityManager.SpawnEntity("Screwdriver", coords);
            wrench = entityManager.SpawnEntity("Wrench", coords);
            wireStack = entityManager.SpawnEntity("CableApcStack", coords);
            cpu = entityManager.SpawnEntity("TreasureCPUSupercharged", coords);
            var cpu2 = entityManager.SpawnEntity("TreasureCPUSupercharged", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            var cyberArm = bodySystem.GetAllOrgans(patient).First(o => entityManager.HasComponent<CyberLimbComponent>(o));
            entityManager.EnsureComponent<CyberneticsMaintenanceComponent>(patient);
            var maint = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            maint.PanelOpen = true;
            maint.BoltsTight = true;
            entityManager.Dirty(patient, maint);
            storageSystem.Insert(cyberArm, cpu, out _, user: null, playSound: false);
            storageSystem.Insert(cyberArm, cpu2, out _, user: null, playSound: false);

            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
        });

        var doAfterTicks = 150;

        await pair.RunTicksSync(5);
        await server.WaitPost(() => { entityManager.EventBus.RaiseLocalEvent(patient, new InteractUsingEvent(technician, screwdriver, patient, coords)); });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            entityManager.EventBus.RaiseLocalEvent(patient, new InteractUsingEvent(technician, wrench, patient, coords));
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wireStack, checkActionBlocker: false);
            entityManager.EventBus.RaiseLocalEvent(patient, new InteractUsingEvent(technician, wireStack, patient, coords));
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            entityManager.EventBus.RaiseLocalEvent(patient, new InteractUsingEvent(technician, wrench, patient, coords));
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            Assert.That(stats.Efficiency, Is.EqualTo(1.2f).Within(0.001f), "2 CPUs = 120% after repair");
        });

        await pair.CleanReturnAsync();
    }
}
