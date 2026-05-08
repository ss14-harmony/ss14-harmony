using System.Linq;
using System.Numerics;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Metabolism;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Xenograft organs apply foreign-host metabolism scaling and integrity penalty; native host clears both.
/// </summary>
[TestFixture]
public sealed class XenograftIntegrationTest
{
    private static EntityUid GetTorso(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Torso") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0));
        return ev.Parts[0];
    }

    private static EntityUid GetHeart(IEntityManager entityManager, BodySystem bodySystem, EntityUid body)
    {
        return bodySystem.GetAllOrgans(body).First(o =>
            entityManager.TryGetComponent(o, out OrganComponent comp) && comp.Category?.Id == "Heart");
    }

    [Test]
    public async Task MonkeyHeart_InHuman_HasPenaltyAndScaledMetabolism_InMonkey_Clear()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var coords = mapData.GridCoords;
            var monkey = entityManager.SpawnEntity("MobMonkey", coords);
            var human = entityManager.SpawnEntity("MobHuman", coords.Offset(new Vector2(2, 0)));
            var monkey2 = entityManager.SpawnEntity("MobMonkey", coords.Offset(new Vector2(4, 0)));

            var heart = GetHeart(entityManager, bodySystem, monkey);

            var removeEv = new OrganRemoveRequestEvent(heart);
            entityManager.EventBus.RaiseLocalEvent(heart, ref removeEv);
            Assert.That(removeEv.Success, Is.True);

            var humanTorso = GetTorso(entityManager, human);
            var humanHeart = GetHeart(entityManager, bodySystem, human);
            var removeHumanHeart = new OrganRemoveRequestEvent(humanHeart);
            entityManager.EventBus.RaiseLocalEvent(humanHeart, ref removeHumanHeart);
            Assert.That(removeHumanHeart.Success, Is.True);

            var insertEv = new OrganInsertRequestEvent(humanTorso, heart);
            entityManager.EventBus.RaiseLocalEvent(humanTorso, ref insertEv);
            Assert.That(insertEv.Success, Is.True);

            Assert.That(entityManager.TryGetComponent(heart, out IntegrityPenaltyComponent ip) && ip.XenograftPenalty > 0,
                "Foreign xenograft should add XenograftPenalty on the organ");

            var healSpec = new DamageSpecifier();
            healSpec.DamageDict[new ProtoId<DamageTypePrototype>("Blunt")] = FixedPoint2.New(-2);
            var healEffect = new HealthChange { Damage = healSpec, IgnoreResistances = true };

            var insertedHeart = GetHeart(entityManager, bodySystem, human);
            var modEv = new GetOrganMetabolismScaleModifierEvent(insertedHeart, healEffect) { Scale = 1f };
            entityManager.EventBus.RaiseLocalEvent(human, ref modEv);
            Assert.That(modEv.Scale, Is.EqualTo(0.6f).Within(0.001f),
                "Default foreign xenograft quality should scale metabolism (0.6)");

            var removeFromHuman = new OrganRemoveRequestEvent(insertedHeart);
            entityManager.EventBus.RaiseLocalEvent(insertedHeart, ref removeFromHuman);
            Assert.That(removeFromHuman.Success, Is.True);

            var monkey2Torso = GetTorso(entityManager, monkey2);
            var monkey2Heart = GetHeart(entityManager, bodySystem, monkey2);
            var removeM2 = new OrganRemoveRequestEvent(monkey2Heart);
            entityManager.EventBus.RaiseLocalEvent(monkey2Heart, ref removeM2);
            Assert.That(removeM2.Success, Is.True);

            var insertNative = new OrganInsertRequestEvent(monkey2Torso, insertedHeart);
            entityManager.EventBus.RaiseLocalEvent(monkey2Torso, ref insertNative);
            Assert.That(insertNative.Success, Is.True);

            Assert.That(entityManager.TryGetComponent(insertedHeart, out IntegrityPenaltyComponent ip2) && ip2.XenograftPenalty == 0,
                "Native-species host should clear xenograft integrity penalty");

            var modEv2 = new GetOrganMetabolismScaleModifierEvent(insertedHeart, healEffect) { Scale = 1f };
            entityManager.EventBus.RaiseLocalEvent(monkey2, ref modEv2);
            Assert.That(modEv2.Scale, Is.EqualTo(1f).Within(0.001f),
                "Native host should not scale metabolism down");
        });

        await pair.CleanReturnAsync();
    }
}
