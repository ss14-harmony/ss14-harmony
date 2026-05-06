using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Monkey brains use the same <see cref="BrainComponent"/> pipeline as human brains and can be moved between heads.
/// Full ghost/mind yanking for monkeys is covered indirectly by <see cref="Mind.BrainReturnToBodyTest"/> on humans;
/// monkeys differ in mob/death behavior enough that we validate implant topology here.
/// </summary>
[TestFixture]
public sealed class MonkeyBrainHarvestIntegrationTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamage = "Blunt";

    private static EntityUid GetHeadPart(IEntityManager em, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Head") };
        em.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Is.Not.Empty);
        return ev.Parts[0];
    }

    private static EntityUid? FindBrain(IEntityManager em, BodySystem bodySys, EntityUid body)
    {
        foreach (var organ in bodySys.GetAllOrgans(body))
        {
            if (em.HasComponent<BrainComponent>(organ))
                return organ;
        }

        return null;
    }

    private static void KillMob(
        IEntityManager em,
        DamageableSystem damageable,
        MobThresholdSystem thresholds,
        IPrototypeManager protoMan,
        EntityUid mob)
    {
        var dmg = em.GetComponent<DamageableComponent>(mob);
        var deathThreshold = thresholds.GetThresholdForState(mob, MobState.Dead);
        damageable.SetDamage((mob, dmg), new DamageSpecifier(protoMan.Index(BluntDamage), deathThreshold));
    }

    [Test]
    public async Task MonkeyBrain_CanExplantAndImplantIntoHumanHead()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var em = server.ResolveDependency<IEntityManager>();
        var bodySys = em.System<BodySystem>();
        var damageable = em.System<DamageableSystem>();
        var thresholds = em.System<MobThresholdSystem>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var monkey = em.SpawnEntity("MobMonkey", mapData.GridCoords);
            var human = em.SpawnEntity("MobHuman", mapData.GridCoords.Offset(new Vector2(2, 0)));

            KillMob(em, damageable, thresholds, protoMan, human);
            Assert.That(em.GetComponent<MobStateComponent>(human).CurrentState, Is.EqualTo(MobState.Dead));

            var monkeyBrain = FindBrain(em, bodySys, monkey);
            Assert.That(monkeyBrain, Is.Not.Null);
            var humanBrain = FindBrain(em, bodySys, human);
            Assert.That(humanBrain, Is.Not.Null);

            var coords = em.GetComponent<TransformComponent>(monkey).Coordinates;

            var rmMonkey = new OrganRemoveRequestEvent(monkeyBrain!.Value) { Destination = coords };
            em.EventBus.RaiseLocalEvent(monkeyBrain.Value, ref rmMonkey);
            Assert.That(rmMonkey.Success, Is.True);

            var rmHuman = new OrganRemoveRequestEvent(humanBrain!.Value) { Destination = coords };
            em.EventBus.RaiseLocalEvent(humanBrain.Value, ref rmHuman);
            Assert.That(rmHuman.Success, Is.True);

            var head = GetHeadPart(em, human);
            var insert = new OrganInsertRequestEvent(head, monkeyBrain.Value);
            em.EventBus.RaiseLocalEvent(head, ref insert);
            Assert.That(insert.Success, Is.True, "Monkey brain should implant into human head");

            var brainNow = FindBrain(em, bodySys, human);
            Assert.That(brainNow, Is.EqualTo(monkeyBrain));
            var meta = em.GetComponent<MetaDataComponent>(brainNow!.Value);
            Assert.That(meta.EntityPrototype?.ID, Is.EqualTo("OrganMonkeyBrain"));
        });

        await pair.CleanReturnAsync();
    }
}
