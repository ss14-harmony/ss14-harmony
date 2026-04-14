using System.Linq;
using Content.IntegrationTests;
using Content.Server.Cybernetics.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(CyberLimbStatsSystem))]
public sealed class CyberLimbBatteryIntegrationTest
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
    public async Task PowerCell_AddsBatteryToStats_WhenInstalled()
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
            var powerCell = entityManager.SpawnEntity("PowerCellMedium", coords);
            Assert.That(storageSystem.Insert(cyberArm, powerCell, out _, user: null, playSound: false), Is.True,
                "Insert PowerCellMedium should succeed");

            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(human);
            Assert.That(stats.BatteryMax, Is.GreaterThan(0f), "BatteryMax should be positive when power cell installed");
            Assert.That(stats.BatteryRemaining, Is.GreaterThan(0f), "BatteryRemaining should be positive when power cell installed");
            Assert.That(stats.BatteryRemaining, Is.LessThanOrEqualTo(stats.BatteryMax),
                "BatteryRemaining should not exceed BatteryMax");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Battery_Drains_OverTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;
        float initialBatteryRemaining = default;

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);

            var cyberArm = bodySystem.GetAllOrgans(human).First(o => entityManager.HasComponent<CyberLimbComponent>(o));
            var powerCell = entityManager.SpawnEntity("PowerCellMedium", coords);
            storageSystem.Insert(cyberArm, powerCell, out _, user: null, playSound: false);

            patient = human;
            initialBatteryRemaining = entityManager.GetComponent<CyberLimbStatsComponent>(human).BatteryRemaining;
        });

        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            Assert.That(stats.BatteryRemaining, Is.LessThan(initialBatteryRemaining),
                "BatteryRemaining should have decreased after ~2.5 seconds");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Depletion_WhenBatteryEmpty_ReducesEfficiency()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var moduleSystem = entityManager.System<CyberLimbModuleSystem>();
        var batterySystem = entityManager.System<SharedBatterySystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);

            var cyberArm = bodySystem.GetAllOrgans(human).First(o => entityManager.HasComponent<CyberLimbComponent>(o));
            var powerCell = entityManager.SpawnEntity("PowerCellMedium", coords);
            storageSystem.Insert(cyberArm, powerCell, out _, user: null, playSound: false);

            patient = human;

            foreach (var battery in moduleSystem.GetBatteryEntities(patient))
            {
                batterySystem.SetCharge(battery, 0f);
            }

            var statsSystem = entityManager.System<CyberLimbStatsSystem>();
            statsSystem.RecomputeAndRefresh(patient);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            Assert.That(stats.Efficiency, Is.EqualTo(0.5f), "Efficiency should be 0.5 when battery depleted");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NoBattery_NoBatteryDisplay_Inspection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var inventorySystem = entityManager.System<InventorySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var coords = mapData.GridCoords;
            var patient = entityManager.SpawnEntity("MobHuman", coords);
            var examiner = entityManager.SpawnEntity("MobHuman", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            Assert.That(inventorySystem.SpawnItemInSlot(examiner, "eyes", "ClothingEyesHudDiagnostic"),
                Is.True, "Should equip diagnostic goggles");

            var msg = new FormattedMessage();
            var ev = new ExaminedEvent(msg, patient, examiner, isInDetailsRange: true, hasDescription: false);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);

            var total = ev.GetTotalMessage();
            var text = total.ToString();
            Assert.That(text, Does.Not.Contain("Battery:"), "Examine text should not contain Battery when no battery in storage");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WithBattery_ShowsBatteryDisplay_Inspection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var inventorySystem = entityManager.System<InventorySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var coords = mapData.GridCoords;
            var patient = entityManager.SpawnEntity("MobHuman", coords);
            var examiner = entityManager.SpawnEntity("MobHuman", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            var cyberArm = bodySystem.GetAllOrgans(patient).First(o => entityManager.HasComponent<CyberLimbComponent>(o));
            var powerCell = entityManager.SpawnEntity("PowerCellMedium", coords);
            storageSystem.Insert(cyberArm, powerCell, out _, user: null, playSound: false);

            Assert.That(inventorySystem.SpawnItemInSlot(examiner, "eyes", "ClothingEyesHudDiagnostic"),
                Is.True, "Should equip diagnostic goggles");

            var msg = new FormattedMessage();
            var ev = new ExaminedEvent(msg, patient, examiner, isInDetailsRange: true, hasDescription: false);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);

            var total = ev.GetTotalMessage();
            var text = total.ToString();
            Assert.That(text, Does.Contain("Battery"), "Examine text should contain Battery with power cell in storage");
            Assert.That(text, Does.Contain("%"), "Examine text should contain percent value for battery");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Capacitor_SlowsBatteryDrain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patientWithCap = default;
        EntityUid patientWithoutCap = default;
        float initialWithCap = default;
        float initialWithoutCap = default;

        await server.WaitAssertion(() =>
        {
            var coords = mapData.GridCoords;

            var humanWithCap = entityManager.SpawnEntity("MobHuman", coords);
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, humanWithCap, coords);
            var cyberArmWithCap = bodySystem.GetAllOrgans(humanWithCap).First(o => entityManager.HasComponent<CyberLimbComponent>(o));
            storageSystem.Insert(cyberArmWithCap, entityManager.SpawnEntity("PowerCellMedium", coords), out _, user: null, playSound: false);
            storageSystem.Insert(cyberArmWithCap, entityManager.SpawnEntity("CapacitorStockPart", coords), out _, user: null, playSound: false);
            patientWithCap = humanWithCap;
            initialWithCap = entityManager.GetComponent<CyberLimbStatsComponent>(humanWithCap).BatteryRemaining;

            var humanWithoutCap = entityManager.SpawnEntity("MobHuman", coords);
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, humanWithoutCap, coords);
            var cyberArmWithoutCap = bodySystem.GetAllOrgans(humanWithoutCap).First(o => entityManager.HasComponent<CyberLimbComponent>(o));
            storageSystem.Insert(cyberArmWithoutCap, entityManager.SpawnEntity("PowerCellMedium", coords), out _, user: null, playSound: false);
            patientWithoutCap = humanWithoutCap;
            initialWithoutCap = entityManager.GetComponent<CyberLimbStatsComponent>(humanWithoutCap).BatteryRemaining;
        });

        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var statsWithCap = entityManager.GetComponent<CyberLimbStatsComponent>(patientWithCap);
            var statsWithoutCap = entityManager.GetComponent<CyberLimbStatsComponent>(patientWithoutCap);

            var drainedWithCap = initialWithCap - statsWithCap.BatteryRemaining;
            var drainedWithoutCap = initialWithoutCap - statsWithoutCap.BatteryRemaining;

            Assert.That(drainedWithCap, Is.LessThan(drainedWithoutCap),
                "Battery drain with capacitor should be less than without capacitor");
        });

        await pair.CleanReturnAsync();
    }
}
