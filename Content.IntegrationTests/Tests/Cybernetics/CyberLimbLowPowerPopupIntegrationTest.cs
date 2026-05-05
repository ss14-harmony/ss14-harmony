using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(CyberLimbStatsSystem))]
public sealed class CyberLimbLowPowerPopupIntegrationTest
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
    public async Task LowBattery_Below25Percent_SetsLowPowerWarned()
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
        var statsSystem = entityManager.System<CyberLimbStatsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);

            var cyberArm = bodySystem.GetAllOrgans(human).First(o => entityManager.HasComponent<CyberLimbComponent>(o));
            var powerCell = entityManager.SpawnEntity("PowerCellMedium", coords);
            Assert.That(storageSystem.Insert(cyberArm, powerCell, out _, user: null, playSound: false), Is.True,
                "Insert PowerCellMedium should succeed");

            foreach (var battery in moduleSystem.GetBatteryEntities(human))
            {
                var bat = entityManager.GetComponent<BatteryComponent>(battery);
                batterySystem.SetCharge(battery, bat.MaxCharge * 0.24f);
            }

            statsSystem.RecomputeAndRefresh(human);
            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(human);
            Assert.That(stats.BatteryMax, Is.GreaterThan(0f), "Precondition: battery max");
            Assert.That(100f * stats.BatteryRemaining / stats.BatteryMax, Is.LessThan(25f),
                "Precondition: battery percent below 25%");

            patient = human;
        });

        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            Assert.That(stats.LowPowerWarned, Is.True,
                "LowPowerWarned should be set after stats tick with battery below 25%");
        });

        await pair.CleanReturnAsync();
    }
}
