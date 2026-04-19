using System.Linq;
using Content.IntegrationTests;
using Content.Server.Cybernetics.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Content.Server.Power.Components;
using Content.Shared.Emp;
using Content.Shared.Interaction;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(CyberneticsBatteryDrainerSystem))]
public sealed class CyberneticsBatteryDrainerIntegrationTest
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
    public async Task Emp_DrainsBattery_ThenDrainFromApc_Recharges()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var storageSystem = entityManager.System<SharedStorageSystem>();
        var empSystem = entityManager.System<SharedEmpSystem>();
        var interactionSystem = entityManager.System<SharedInteractionSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid player = default;
        EntityUid apc = default;
        EntityCoordinates coords = default;

        await server.WaitAssertion(() =>
        {
            coords = mapData.GridCoords;
            player = entityManager.SpawnEntity("MobHuman", coords);
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, player, coords);

            var cyberArm = bodySystem.GetAllOrgans(player).First(o => entityManager.HasComponent<CyberLimbComponent>(o));
            var powerCell = entityManager.SpawnEntity("PowerCellMedium", coords);
            storageSystem.Insert(cyberArm, powerCell, out _, user: null, playSound: false);

            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(player);
            Assert.That(stats.BatteryMax, Is.GreaterThan(0f), "BatteryMax should be positive");
            Assert.That(stats.BatteryRemaining, Is.GreaterThan(0f), "BatteryRemaining should be positive before EMP");

            // Drain batteries with EMP (energyConsumption drains battery charge)
            empSystem.EmpPulse(coords, range: 3f, energyConsumption: 1000f, TimeSpan.FromSeconds(1), user: null);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(player);
            Assert.That(stats.BatteryRemaining, Is.LessThanOrEqualTo(1f),
                "Battery should be depleted after EMP (allowing small float tolerance)");
            Assert.That(stats.Efficiency, Is.EqualTo(0.5f), "Efficiency should be 0.5 when battery depleted");

            // Spawn full APC near player
            apc = entityManager.SpawnEntity("APCBasic", coords);
            Assert.That(entityManager.HasComponent<PowerNetworkBatteryComponent>(apc),
                "APC should have PowerNetworkBatteryComponent");

            // Trigger hand interaction with APC (starts drain DoAfter)
            interactionSystem.InteractHand(player, apc);
        });

        // DoAfter takes 1 second; run ~70 ticks at 60 tps
        await pair.RunTicksSync(70);

        await server.WaitAssertion(() =>
        {
            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(player);
            Assert.That(stats.BatteryRemaining, Is.GreaterThan(0f),
                "Battery should have charge after draining from APC");
            Assert.That(stats.Efficiency, Is.EqualTo(1f), "Efficiency should be 1 when battery has charge");
        });

        await pair.CleanReturnAsync();
    }
}
