using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(Content.Shared.Cybernetics.Systems.CyberLimbAppearanceSystem))]
public sealed class CyberLimbAppearanceAndHandIntegrationTest
{
    private static EntityUid GetLimbByCategory(IEntityManager entityManager, EntityUid body, string category)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static void ReplaceArmWithCyberArm(IEntityManager entityManager, BodySystem bodySystem,
        SharedContainerSystem containerSystem, EntityUid body, EntityCoordinates coords)
    {
        var arm = GetLimbByCategory(entityManager, body, "ArmLeft");
        var removeEv = new OrganRemoveRequestEvent(arm) { Destination = coords };
        entityManager.EventBus.RaiseLocalEvent(arm, ref removeEv);
        Assert.That(removeEv.Success, Is.True, "Remove arm should succeed");

        var cyberArm = entityManager.SpawnEntity("OrganCyberArmLeft", coords);
        var bodyComp = entityManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have Organs container");
        Assert.That(containerSystem.Insert(cyberArm, bodyComp.Organs!), Is.True, "Insert cyber arm should succeed");
    }

    private static void ReplaceLegWithCyberLeg(IEntityManager entityManager, BodySystem bodySystem,
        SharedContainerSystem containerSystem, EntityUid body, EntityCoordinates coords)
    {
        var leg = GetLimbByCategory(entityManager, body, "LegLeft");
        var removeEv = new OrganRemoveRequestEvent(leg) { Destination = coords };
        entityManager.EventBus.RaiseLocalEvent(leg, ref removeEv);
        Assert.That(removeEv.Success, Is.True, "Remove leg should succeed");

        var cyberLeg = entityManager.SpawnEntity("OrganCyberLegLeft", coords);
        var bodyComp = entityManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have Organs container");
        Assert.That(containerSystem.Insert(cyberLeg, bodyComp.Organs!), Is.True, "Insert cyber leg should succeed");
    }

    [Test]
    public async Task CyberArm_AppearanceUpdates_WhenAttached()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid user = default;

        await server.WaitAssertion(() =>
        {
            user = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, user, mapData.GridCoords);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.TryGetComponent<HumanoidProfileComponent>(user, out _), Is.True,
                "User should have HumanoidProfileComponent");

            var arm = GetLimbByCategory(entityManager, user, "ArmLeft");
            Assert.That(entityManager.TryGetComponent(arm, out VisualOrganComponent? visualOrgan), Is.True,
                "Cyber arm should have VisualOrganComponent");
            Assert.That(visualOrgan!.Layer, Is.EqualTo(HumanoidVisualLayers.LArm),
                "Cyber arm should map to LArm layer");
            Assert.That(visualOrgan.Data.State, Is.EqualTo("l_arm-combined-hand"),
                "Cyber arm should use l_arm-combined-hand sprite state");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyberArm_HandSlotWorks_WhenAttached()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid screwdriver = default;

        await server.WaitAssertion(() =>
        {
            user = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, user, mapData.GridCoords);

            Assert.That(handsSystem.GetHandCount(user), Is.EqualTo(2),
                "User should have 2 hands (left and right) after cyber arm attach");

            screwdriver = entityManager.SpawnEntity("Screwdriver", mapData.GridCoords);
            Assert.That(handsSystem.TryPickup(user, screwdriver, "left"), Is.True,
                "Should be able to pick up screwdriver with left hand");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(handsSystem.TryGetHeldItem(user, "left", out var held), Is.True,
                "Left hand should hold an entity");
            Assert.That(held, Is.EqualTo(screwdriver),
                "Left hand should hold the screwdriver");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyberLeg_AppearanceUpdates_WhenAttached()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid user = default;

        await server.WaitAssertion(() =>
        {
            user = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            ReplaceLegWithCyberLeg(entityManager, bodySystem, containerSystem, user, mapData.GridCoords);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.TryGetComponent<HumanoidProfileComponent>(user, out _), Is.True,
                "User should have HumanoidProfileComponent");

            var leg = GetLimbByCategory(entityManager, user, "LegLeft");
            Assert.That(entityManager.TryGetComponent(leg, out VisualOrganComponent? visualOrgan), Is.True,
                "Cyber leg should have VisualOrganComponent");
            Assert.That(visualOrgan!.Layer, Is.EqualTo(HumanoidVisualLayers.LLeg),
                "Cyber leg should map to LLeg layer");
            Assert.That(visualOrgan.Data.State, Is.EqualTo("l_leg-combined-foot"),
                "Cyber leg should use l_leg-combined-foot sprite state");
        });

        await pair.CleanReturnAsync();
    }
}
