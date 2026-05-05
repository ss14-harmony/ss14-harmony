using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

/// <summary>
/// Military cyber limbs grant additive Brute + Heat damage resistance via CyberLimbStatsComponent.CyberDamageResistance.
/// </summary>
[TestFixture]
[TestOf(typeof(CyberLimbStatsSystem))]
public sealed class MilitaryCyberLimbDamageResistanceIntegrationTest
{
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";
    private static readonly ProtoId<DamageTypePrototype> Shock = "Shock";

    private static EntityUid GetArmLeft(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("ArmLeft") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static EntityUid GetLegLeft(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("LegLeft") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static void ReplaceArmWithCyberArm(IEntityManager entityManager,
        SharedContainerSystem containerSystem, EntityUid body, EntityCoordinates coords, string limbId)
    {
        var arm = GetArmLeft(entityManager, body);
        var removeEv = new OrganRemoveRequestEvent(arm) { Destination = coords };
        entityManager.EventBus.RaiseLocalEvent(arm, ref removeEv);
        Assert.That(removeEv.Success, Is.True, "Remove arm should succeed");

        var cyberArm = entityManager.SpawnEntity(limbId, coords);
        var bodyComp = entityManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have Organs container");
        Assert.That(containerSystem.Insert(cyberArm, bodyComp.Organs!), Is.True, "Insert cyber arm should succeed");
    }

    private static void ReplaceLegWithCyberLeg(IEntityManager entityManager,
        SharedContainerSystem containerSystem, EntityUid body, EntityCoordinates coords, string limbId)
    {
        var leg = GetLegLeft(entityManager, body);
        var removeEv = new OrganRemoveRequestEvent(leg) { Destination = coords };
        entityManager.EventBus.RaiseLocalEvent(leg, ref removeEv);
        Assert.That(removeEv.Success, Is.True, "Remove leg should succeed");

        var cyberLeg = entityManager.SpawnEntity(limbId, coords);
        var bodyComp = entityManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have Organs container");
        Assert.That(containerSystem.Insert(cyberLeg, bodyComp.Organs!), Is.True, "Insert cyber leg should succeed");
    }

    private static FixedPoint2 BluntDelta(IEntityManager em, DamageableSystem dmg, EntityUid target,
        ProtoId<DamageTypePrototype> bluntId, DamageSpecifier spec)
    {
        var before = dmg.GetDamageOfType(target, bluntId);
        dmg.TryChangeDamage(target, spec);
        var after = dmg.GetDamageOfType(target, bluntId);
        return after - before;
    }

    [Test]
    public async Task NoMilitaryLimb_NoResistanceApplied()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var em = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var dmg = em.System<DamageableSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = em.SpawnEntity("MobHuman", mapData.GridCoords);
            var bluntProto = protos.Index(Blunt);
            var spec = new DamageSpecifier(bluntProto, FixedPoint2.New(20));
            var delta = BluntDelta(em, dmg, human, Blunt, spec);
            Assert.That(delta, Is.EqualTo(FixedPoint2.New(20)), "MobHuman without military limbs should take full Blunt");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OneMilitaryArm_FivePercentBruteReduction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var em = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var containerSystem = em.System<SharedContainerSystem>();
        var dmg = em.System<DamageableSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = em.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = em.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(em, containerSystem, human, coords, "OrganCyberArmLeftMilitaryT1");

            var stats = em.GetComponent<CyberLimbStatsComponent>(human);
            Assert.That(stats.CyberDamageResistance, Is.EqualTo(0.05f).Within(1e-5f));

            var bluntProto = protos.Index(Blunt);
            var spec = new DamageSpecifier(bluntProto, FixedPoint2.New(20));
            var delta = BluntDelta(em, dmg, human, Blunt, spec);
            Assert.That(delta, Is.EqualTo(FixedPoint2.New(19)), "5% resistance should leave 19 Blunt from 20 dealt");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TwoMilitaryLimbs_AdditiveStacking()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var em = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var containerSystem = em.System<SharedContainerSystem>();
        var dmg = em.System<DamageableSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = em.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = em.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(em, containerSystem, human, coords, "OrganCyberArmLeftMilitaryT1");
            ReplaceLegWithCyberLeg(em, containerSystem, human, coords, "OrganCyberLegLeftMilitaryT1");

            var stats = em.GetComponent<CyberLimbStatsComponent>(human);
            Assert.That(stats.CyberDamageResistance, Is.EqualTo(0.10f).Within(1e-5f));

            var slashProto = protos.Index(Slash);
            var before = dmg.GetDamageOfType(human, Slash);
            dmg.TryChangeDamage(human, new DamageSpecifier(slashProto, FixedPoint2.New(20)));
            var after = dmg.GetDamageOfType(human, Slash);
            Assert.That(after - before, Is.EqualTo(FixedPoint2.New(18)), "10% resistance should leave 18 Slash from 20 dealt");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HealResistance_DoesNotReduceHealing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var em = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var containerSystem = em.System<SharedContainerSystem>();
        var dmg = em.System<DamageableSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = em.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = em.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(em, containerSystem, human, coords, "OrganCyberArmLeftMilitaryT1");

            var bluntProto = protos.Index(Blunt);
            BluntDelta(em, dmg, human, Blunt, new DamageSpecifier(bluntProto, FixedPoint2.New(20)));
            var mid = dmg.GetDamageOfType(human, Blunt);
            Assert.That(mid, Is.EqualTo(FixedPoint2.New(19)), "Setup: 19 Blunt after resisted damage");

            dmg.TryChangeDamage(human, new DamageSpecifier(bluntProto, FixedPoint2.New(-10)));
            var final = dmg.GetDamageOfType(human, Blunt);
            Assert.That(final, Is.EqualTo(FixedPoint2.New(9)), "Healing should apply fully (negative amounts not resisted)");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonAffectedDamageType_Unmodified()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var em = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var containerSystem = em.System<SharedContainerSystem>();
        var dmg = em.System<DamageableSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = em.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = em.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(em, containerSystem, human, coords, "OrganCyberArmLeftMilitaryT1");

            var shockProto = protos.Index(Shock);
            var before = dmg.GetDamageOfType(human, Shock);
            dmg.TryChangeDamage(human, new DamageSpecifier(shockProto, FixedPoint2.New(20)));
            var after = dmg.GetDamageOfType(human, Shock);
            Assert.That(after - before, Is.EqualTo(FixedPoint2.New(20)), "Shock should not be reduced by military plating");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ResistanceSurvivesPowerDepletion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var em = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var bodySystem = em.System<BodySystem>();
        var containerSystem = em.System<SharedContainerSystem>();
        var storageSystem = em.System<SharedStorageSystem>();
        var moduleSystem = em.System<CyberLimbModuleSystem>();
        var batterySystem = em.System<SharedBatterySystem>();
        var statsSystem = em.System<CyberLimbStatsSystem>();
        var dmg = em.System<DamageableSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = em.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = em.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(em, containerSystem, human, coords, "OrganCyberArmLeftMilitaryT1");

            var cyberArm = bodySystem.GetAllOrgans(human).First(o => em.HasComponent<MilitaryCyberLimbComponent>(o));
            var powerCell = em.SpawnEntity("PowerCellMedium", coords);
            Assert.That(storageSystem.Insert(cyberArm, powerCell, out _, user: null, playSound: false), Is.True);

            foreach (var battery in moduleSystem.GetBatteryEntities(human))
                batterySystem.SetCharge(battery, 0f);

            statsSystem.RecomputeAndRefresh(human);

            var stats = em.GetComponent<CyberLimbStatsComponent>(human);
            Assert.That(stats.CyberDamageResistance, Is.EqualTo(0.05f).Within(1e-5f),
                "Structural resistance must persist when battery is depleted");
            Assert.That(stats.ArmEfficiency, Is.EqualTo(0.5f), "Sanity: depleted efficiency");

            var bluntProto = protos.Index(Blunt);
            var delta = BluntDelta(em, dmg, human, Blunt, new DamageSpecifier(bluntProto, FixedPoint2.New(20)));
            Assert.That(delta, Is.EqualTo(FixedPoint2.New(19)), "Brute resist should still apply when out of power");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DetachingLimbRemovesResistance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var em = server.ResolveDependency<IEntityManager>();
        var containerSystem = em.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = em.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = em.GetComponent<TransformComponent>(human).Coordinates;
            ReplaceArmWithCyberArm(em, containerSystem, human, coords, "OrganCyberArmLeftMilitaryT1");

            var cyberArm = em.System<BodySystem>().GetAllOrgans(human)
                .First(o => em.HasComponent<MilitaryCyberLimbComponent>(o));
            var removeEv = new OrganRemoveRequestEvent(cyberArm) { Destination = coords };
            em.EventBus.RaiseLocalEvent(cyberArm, ref removeEv);
            Assert.That(removeEv.Success, Is.True);

            Assert.That(em.HasComponent<CyberLimbStatsComponent>(human), Is.False,
                "No cyber limbs left — stats component should be removed");
        });

        await pair.CleanReturnAsync();
    }
}
