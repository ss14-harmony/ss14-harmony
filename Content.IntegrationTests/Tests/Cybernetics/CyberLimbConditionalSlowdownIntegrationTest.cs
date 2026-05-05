using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(CyberLimbStatsSystem))]
public sealed partial class CyberLimbConditionalSlowdownIntegrationTest
{
    [Serializable, NetSerializable]
    private sealed partial class CyberSlowdownTestDoAfterEvent : DoAfterEvent
    {
        public override DoAfterEvent Clone() => this;
    }

    private static EntityUid GetPart(IEntityManager entityManager, EntityUid body, string category)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static void ReplacePartWithCyber(IEntityManager entityManager, SharedContainerSystem containerSystem,
        EntityUid body, string category, string cyberPrototype, EntityCoordinates coords)
    {
        var part = GetPart(entityManager, body, category);
        var removeEv = new OrganRemoveRequestEvent(part) { Destination = coords };
        entityManager.EventBus.RaiseLocalEvent(part, ref removeEv);
        Assert.That(removeEv.Success, Is.True, $"Remove {category} should succeed");

        var cyber = entityManager.SpawnEntity(cyberPrototype, coords);
        var bodyComp = entityManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have Organs container");
        Assert.That(containerSystem.Insert(cyber, bodyComp.Organs!), Is.True, $"Insert {cyberPrototype} should succeed");
    }

    private static void ForceDepleted(IEntityManager entityManager, MovementSpeedModifierSystem movementSpeedSystem, EntityUid body)
    {
        var stats = entityManager.GetComponent<CyberLimbStatsComponent>(body);
        stats.ServiceTimeRemaining = TimeSpan.Zero;
        stats.ArmEfficiency = 0.5f;
        stats.LegEfficiency = 0.5f;
        entityManager.Dirty(body, stats);
        movementSpeedSystem.RefreshMovementSpeedModifiers(body);
    }

    [Test]
    public async Task MovementSlowdown_DoesNotApply_WithoutCyberLegs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var movementSpeedSystem = entityManager.System<MovementSpeedModifierSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(patient).Coordinates;

            ReplacePartWithCyber(entityManager, containerSystem, patient, "ArmLeft", "OrganCyberArmLeft", coords);

            Assert.That(entityManager.HasComponent<CyberLimbStatsComponent>(patient), Is.True,
                "Patient should have CyberLimbStatsComponent after cyber arm install");

            ForceDepleted(entityManager, movementSpeedSystem, patient);

            Assert.That(entityManager.HasComponent<MovementSpeedModifierComponent>(patient), Is.True,
                "Patient should have MovementSpeedModifierComponent after refresh");
            var moveComp = entityManager.GetComponent<MovementSpeedModifierComponent>(patient);
            Assert.That(moveComp.WalkSpeedModifier, Is.EqualTo(1f),
                "WalkSpeedModifier should be unchanged when body has no cyber legs, even with a depleted cyber arm");
            Assert.That(moveComp.SprintSpeedModifier, Is.EqualTo(1f),
                "SprintSpeedModifier should be unchanged when body has no cyber legs, even with a depleted cyber arm");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MovementSlowdown_Applies_WithCyberLegs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var movementSpeedSystem = entityManager.System<MovementSpeedModifierSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(patient).Coordinates;

            ReplacePartWithCyber(entityManager, containerSystem, patient, "LegLeft", "OrganCyberLegLeft", coords);

            Assert.That(entityManager.HasComponent<CyberLimbStatsComponent>(patient), Is.True,
                "Patient should have CyberLimbStatsComponent after cyber leg install");

            ForceDepleted(entityManager, movementSpeedSystem, patient);

            var moveComp = entityManager.GetComponent<MovementSpeedModifierComponent>(patient);
            Assert.That(moveComp.WalkSpeedModifier, Is.EqualTo(0.5f),
                "WalkSpeedModifier should be 0.5 when body has a depleted cyber leg");
            Assert.That(moveComp.SprintSpeedModifier, Is.EqualTo(0.5f),
                "SprintSpeedModifier should be 0.5 when body has a depleted cyber leg");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InteractSlowdown_DoesNotApply_WithoutCyberArms()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var movementSpeedSystem = entityManager.System<MovementSpeedModifierSystem>();
        var doAfterSystem = entityManager.System<SharedDoAfterSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(patient).Coordinates;

            ReplacePartWithCyber(entityManager, containerSystem, patient, "LegLeft", "OrganCyberLegLeft", coords);
            ForceDepleted(entityManager, movementSpeedSystem, patient);

            var baseDelay = TimeSpan.FromSeconds(2);
            var ev = new CyberSlowdownTestDoAfterEvent();
            var args = new DoAfterArgs(entityManager, patient, baseDelay, ev, patient) { Broadcast = true };
            Assert.That(doAfterSystem.TryStartDoAfter(args), Is.True, "TryStartDoAfter should succeed");

            var doAfterComp = entityManager.GetComponent<DoAfterComponent>(patient);
            var started = doAfterComp.DoAfters.Values.Single();
            Assert.That(started.Args.Delay, Is.EqualTo(baseDelay),
                "DoAfter delay should be unchanged when body has no cyber arms, even with a depleted cyber leg");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InteractSlowdown_Applies_WithCyberArms()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var movementSpeedSystem = entityManager.System<MovementSpeedModifierSystem>();
        var doAfterSystem = entityManager.System<SharedDoAfterSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(patient).Coordinates;

            ReplacePartWithCyber(entityManager, containerSystem, patient, "ArmLeft", "OrganCyberArmLeft", coords);
            ForceDepleted(entityManager, movementSpeedSystem, patient);

            var baseDelay = TimeSpan.FromSeconds(2);
            var ev = new CyberSlowdownTestDoAfterEvent();
            var args = new DoAfterArgs(entityManager, patient, baseDelay, ev, patient) { Broadcast = true };
            Assert.That(doAfterSystem.TryStartDoAfter(args), Is.True, "TryStartDoAfter should succeed");

            var doAfterComp = entityManager.GetComponent<DoAfterComponent>(patient);
            var started = doAfterComp.DoAfters.Values.Single();
            Assert.That(started.Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(4)),
                "DoAfter delay should be doubled when body has a depleted cyber arm (50% interact slowdown)");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InteractSlowdown_DoesNotApply_WhenNotDepleted()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var doAfterSystem = entityManager.System<SharedDoAfterSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(patient).Coordinates;

            ReplacePartWithCyber(entityManager, containerSystem, patient, "ArmLeft", "OrganCyberArmLeft", coords);

            var stats = entityManager.GetComponent<CyberLimbStatsComponent>(patient);
            Assert.That(stats.ArmEfficiency, Is.EqualTo(1f),
                "ArmEfficiency should be 1 when not depleted and no CPUs installed");

            var baseDelay = TimeSpan.FromSeconds(2);
            var ev = new CyberSlowdownTestDoAfterEvent();
            var args = new DoAfterArgs(entityManager, patient, baseDelay, ev, patient) { Broadcast = true };
            Assert.That(doAfterSystem.TryStartDoAfter(args), Is.True, "TryStartDoAfter should succeed");

            var doAfterComp = entityManager.GetComponent<DoAfterComponent>(patient);
            var started = doAfterComp.DoAfters.Values.Single();
            Assert.That(started.Args.Delay, Is.EqualTo(baseDelay),
                "DoAfter delay should be unchanged when cybernetics are not depleted");
        });

        await pair.CleanReturnAsync();
    }
}
