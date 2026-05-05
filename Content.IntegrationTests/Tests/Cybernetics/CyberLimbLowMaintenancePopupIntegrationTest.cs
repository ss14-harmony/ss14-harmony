using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(CyberLimbStatsSystem))]
public sealed class CyberLimbLowMaintenancePopupIntegrationTest
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
    public async Task LowService_Below25Percent_SetsLowMaintenanceWarned()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var statsSystem = entityManager.System<CyberLimbStatsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);

            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(human);
            // One limb: max service ~5 min (300s). 60s remaining is 20% — below 25% threshold.
            stats.BaseServiceRemaining = TimeSpan.FromSeconds(60);
            statsSystem.RecomputeAndRefresh(human);

            Assert.That(stats.ServiceTimeMax, Is.GreaterThan(TimeSpan.Zero), "Precondition: max service time");
            Assert.That(stats.ServiceTimeRemaining.TotalSeconds / stats.ServiceTimeMax.TotalSeconds,
                Is.LessThan(0.25), "Precondition: remaining fraction below 25%");

            patient = human;
        });

        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            Assert.That(stats.LowMaintenanceWarned, Is.True,
                "LowMaintenanceWarned should be set after stats tick with service below 25%");
        });

        await pair.CleanReturnAsync();
    }
}
