using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(CyberLimbInspectionSystem))]
public sealed class CyberLimbInspectionIntegrationTest
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
    public async Task WithDiagnosticGoggles_ExamineShowsCyberLimbStats()
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
            Assert.That(entityManager.HasComponent<CyberLimbStatsComponent>(patient), Is.True,
                "Patient should have CyberLimbStatsComponent");

            Assert.That(inventorySystem.SpawnItemInSlot(examiner, "eyes", "ClothingEyesHudDiagnostic"),
                Is.True, "Should equip diagnostic goggles");

            var msg = new FormattedMessage();
            var ev = new ExaminedEvent(msg, patient, examiner, isInDetailsRange: true, hasDescription: false);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);

            var total = ev.GetTotalMessage();
            var text = total.ToString();
            Assert.That(text, Does.Contain("Service time"), "Examine text should contain service time with diagnostic goggles");
            Assert.That(text, Does.Contain("Efficiency"), "Examine text should contain efficiency with diagnostic goggles");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WithDiagnosticGoggles_ExamineShowsInstalledModules()
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
            var cyberArm = bodySystem.GetAllOrgans(patient).First(o =>
                entityManager.HasComponent<CyberLimbComponent>(o));

            var screwdriver = entityManager.SpawnEntity("Screwdriver", coords);
            storageSystem.Insert(cyberArm, screwdriver, out _, user: null, playSound: false);

            Assert.That(inventorySystem.SpawnItemInSlot(examiner, "eyes", "ClothingEyesHudDiagnostic"),
                Is.True, "Should equip diagnostic goggles");

            var msg = new FormattedMessage();
            var ev = new ExaminedEvent(msg, patient, examiner, isInDetailsRange: true, hasDescription: false);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);

            var total = ev.GetTotalMessage();
            var text = total.ToString();
            Assert.That(text, Does.Contain("screwdriver").IgnoreCase, "Examine text should contain installed module name");
            Assert.That(text, Does.Contain("Installed modules"), "Examine text should contain modules label");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WithoutDiagnosticGoggles_ExamineDoesNotShowCyberLimbStats()
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
            var coords = mapData.GridCoords;
            var patient = entityManager.SpawnEntity("MobHuman", coords);
            var examiner = entityManager.SpawnEntity("MobHuman", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            Assert.That(entityManager.HasComponent<CyberLimbStatsComponent>(patient), Is.True,
                "Patient should have CyberLimbStatsComponent");

            var msg = new FormattedMessage();
            var ev = new ExaminedEvent(msg, patient, examiner, isInDetailsRange: true, hasDescription: false);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);

            var total = ev.GetTotalMessage();
            var text = total.ToString();
            Assert.That(text, Does.Not.Contain("Service time"), "Examine text should not contain cyber limb stats without diagnostic goggles");
        });

        await pair.CleanReturnAsync();
    }
}
