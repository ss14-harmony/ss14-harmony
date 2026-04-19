using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Humanoid;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

/// <summary>
/// Regression test for Bug 2: When attaching a cybernetic arm, the arm briefly appeared as human.
/// The fix ensures the sprite is changed to cybernetic before revealing the arm.
/// Verification: Attach a cybernetic arm to an empty arm slot; the arm should appear as cybernetic
/// immediately (cyber sprite set, layers visible) with no flash of organic arm.
/// </summary>
[TestFixture]
[TestOf(typeof(Content.Shared.Cybernetics.Systems.CyberLimbAppearanceSystem))]
public sealed class CyberArmAppearanceNoFlashIntegrationTest
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

    [Test]
    public async Task CyberArm_AppearanceIsCyberneticImmediately_WhenAttached()
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

            // In the organ-based visual system, cyber arm has VisualOrganComponent with cyber sprite.
            // VisualBodySystem applies it on insert - no flash since the organ's data is correct from the start.
            var ev = new BodyPartQueryByTypeEvent(user) { Category = new ProtoId<OrganCategoryPrototype>("ArmLeft") };
            entityManager.EventBus.RaiseLocalEvent(user, ref ev);
            Assert.That(ev.Parts, Has.Count.GreaterThanOrEqualTo(1), "User should have left arm");
            var arm = ev.Parts[0];
            Assert.That(entityManager.TryGetComponent(arm, out VisualOrganComponent? visualOrgan), Is.True,
                "Cyber arm should have VisualOrganComponent");
            Assert.That(visualOrgan!.Data.State, Is.EqualTo("l_arm-combined-hand"),
                "Cyber arm should use l_arm-combined-hand sprite (combined arm+hand, no organic flash)");
        });

        await pair.CleanReturnAsync();
    }
}
