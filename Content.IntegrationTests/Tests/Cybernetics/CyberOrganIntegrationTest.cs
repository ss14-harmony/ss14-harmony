#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects.Effects.Body;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Flash;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Surgery.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(CyberOrganComponent))]
public sealed class CyberOrganIntegrationTest
{
    private static EntityUid GetTorso(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Torso") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static EntityUid GetHead(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Head") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static EntityUid GetHeart(IEntityManager entityManager, BodySystem bodySystem, EntityUid body)
    {
        return bodySystem.GetAllOrgans(body).First(o =>
            entityManager.TryGetComponent(o, out OrganComponent? comp) && comp.Category?.Id == "Heart");
    }

    private static EntityUid GetOrganByCategoryId(IEntityManager entityManager, BodySystem bodySystem, EntityUid body, string categoryId)
    {
        return bodySystem.GetAllOrgans(body).First(o =>
            entityManager.TryGetComponent(o, out OrganComponent? comp) && comp.Category?.Id == categoryId);
    }

    [Test]
    public async Task CyberOrgans_ExcludedFromCyberLimbTotals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso = GetTorso(entityManager, human);
            var heart = GetHeart(entityManager, bodySystem, human);

            var removeEv = new OrganRemoveRequestEvent(heart);
            entityManager.EventBus.RaiseLocalEvent(heart, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove heart should succeed");

            var cyberHeart = entityManager.SpawnEntity("OrganCyberHeartBasic", entityManager.GetComponent<TransformComponent>(human).Coordinates);
            var insertEv = new OrganInsertRequestEvent(torso, cyberHeart);
            entityManager.EventBus.RaiseLocalEvent(torso, ref insertEv);
            Assert.That(insertEv.Success, Is.True, "Insert cyber heart should succeed");

            Assert.That(entityManager.HasComponent<CyberLimbStatsComponent>(human), Is.False,
                "Body with only cyber organs should NOT have CyberLimbStatsComponent");
            Assert.That(entityManager.HasComponent<CyberneticsMaintenanceComponent>(human), Is.False,
                "Body with only cyber organs should NOT have CyberneticsMaintenanceComponent");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyberOrgans_CountTowardIntegrity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso = GetTorso(entityManager, human);
            var heart = GetHeart(entityManager, bodySystem, human);

            var removeEv = new OrganRemoveRequestEvent(heart);
            entityManager.EventBus.RaiseLocalEvent(heart, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove heart should succeed");

            var cyberHeart = entityManager.SpawnEntity("OrganCyberHeartBasic", entityManager.GetComponent<TransformComponent>(human).Coordinates);
            var insertEv = new OrganInsertRequestEvent(torso, cyberHeart);
            entityManager.EventBus.RaiseLocalEvent(torso, ref insertEv);
            Assert.That(insertEv.Success, Is.True, "Insert cyber heart should succeed");

            Assert.That(entityManager.TryGetComponent(human, out IntegrityUsageComponent? usageComp), Is.True,
                "Body should have IntegrityUsageComponent after inserting cyber organ");
            Assert.That(usageComp!.Usage, Is.EqualTo(1), "Usage should be 1 after inserting cyber heart with integrityCost 1");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyberOrgan_Insertion_SucceedsWithCorrectEffectiveness()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso = GetTorso(entityManager, human);
            var heart = GetHeart(entityManager, bodySystem, human);

            var removeEv = new OrganRemoveRequestEvent(heart);
            entityManager.EventBus.RaiseLocalEvent(heart, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove heart should succeed");

            var cyberHeart = entityManager.SpawnEntity("OrganCyberHeartBasic", entityManager.GetComponent<TransformComponent>(human).Coordinates);
            Assert.That(entityManager.TryGetComponent(cyberHeart, out CyberOrganComponent? cyberComp), Is.True,
                "Cyber heart should have CyberOrganComponent");
            Assert.That(cyberComp!.Effectiveness, Is.EqualTo(0.8f), "Basic cyber heart should have 80% effectiveness");

            var insertEv = new OrganInsertRequestEvent(torso, cyberHeart);
            entityManager.EventBus.RaiseLocalEvent(torso, ref insertEv);
            Assert.That(insertEv.Success, Is.True, "Insert cyber heart should succeed");

            var insertedHeart = bodySystem.GetAllOrgans(human).First(o =>
                entityManager.TryGetComponent(o, out OrganComponent? oc) && oc.Category?.Id == "Heart");
            Assert.That(entityManager.TryGetComponent(insertedHeart, out CyberOrganComponent? insertedCyber), Is.True,
                "Inserted organ should have CyberOrganComponent");
            Assert.That(insertedCyber!.Effectiveness, Is.EqualTo(0.8f), "Inserted cyber heart should retain 80% effectiveness");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyberOrgan_HeartT2_MetabolismModifier_ScalesHealing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso = GetTorso(entityManager, human);
            var heart = GetHeart(entityManager, bodySystem, human);

            var removeEv = new OrganRemoveRequestEvent(heart);
            entityManager.EventBus.RaiseLocalEvent(heart, ref removeEv);
            Assert.That(removeEv.Success, Is.True);

            var cyberHeart = entityManager.SpawnEntity("OrganCyberHeartT2", entityManager.GetComponent<TransformComponent>(human).Coordinates);
            var insertEv = new OrganInsertRequestEvent(torso, cyberHeart);
            entityManager.EventBus.RaiseLocalEvent(torso, ref insertEv);
            Assert.That(insertEv.Success, Is.True);

            var insertedHeart = GetHeart(entityManager, bodySystem, human);

            var healSpec = new DamageSpecifier();
            healSpec.DamageDict[new ProtoId<DamageTypePrototype>("Blunt")] = FixedPoint2.New(-2);
            var healEffect = new HealthChange { Damage = healSpec, IgnoreResistances = true };

            var modEv = new GetOrganMetabolismScaleModifierEvent(insertedHeart, healEffect) { Scale = 1f };
            entityManager.EventBus.RaiseLocalEvent(human, ref modEv);
            Assert.That(modEv.Scale, Is.EqualTo(1.2f).Within(0.001f), "T2 cyber heart should scale healing by effectiveness");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyberOrgan_StomachT2_MetabolismModifier_ScalesHealing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso = GetTorso(entityManager, human);
            var stomach = GetOrganByCategoryId(entityManager, bodySystem, human, "Stomach");

            var removeEv = new OrganRemoveRequestEvent(stomach);
            entityManager.EventBus.RaiseLocalEvent(stomach, ref removeEv);
            Assert.That(removeEv.Success, Is.True);

            var cyberStomach = entityManager.SpawnEntity("OrganCyberStomachT2", entityManager.GetComponent<TransformComponent>(human).Coordinates);
            var insertEv = new OrganInsertRequestEvent(torso, cyberStomach);
            entityManager.EventBus.RaiseLocalEvent(torso, ref insertEv);
            Assert.That(insertEv.Success, Is.True);

            var insertedStomach = GetOrganByCategoryId(entityManager, bodySystem, human, "Stomach");

            var healSpec = new DamageSpecifier();
            healSpec.DamageDict[new ProtoId<DamageTypePrototype>("Blunt")] = FixedPoint2.New(-1);
            var healEffect = new HealthChange { Damage = healSpec, IgnoreResistances = true };

            var modEv = new GetOrganMetabolismScaleModifierEvent(insertedStomach, healEffect) { Scale = 1f };
            entityManager.EventBus.RaiseLocalEvent(human, ref modEv);
            Assert.That(modEv.Scale, Is.EqualTo(1.2f).Within(0.001f), "T2 cyber stomach should scale healing by effectiveness");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyberOrgan_LungsT2_MetabolismModifier_ScalesModifyLungGas()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso = GetTorso(entityManager, human);
            var lungs = GetOrganByCategoryId(entityManager, bodySystem, human, "Lungs");

            var removeEv = new OrganRemoveRequestEvent(lungs);
            entityManager.EventBus.RaiseLocalEvent(lungs, ref removeEv);
            Assert.That(removeEv.Success, Is.True);

            var cyberLungs = entityManager.SpawnEntity("OrganCyberLungsT2", entityManager.GetComponent<TransformComponent>(human).Coordinates);
            var insertEv = new OrganInsertRequestEvent(torso, cyberLungs);
            entityManager.EventBus.RaiseLocalEvent(torso, ref insertEv);
            Assert.That(insertEv.Success, Is.True);

            var insertedLungs = GetOrganByCategoryId(entityManager, bodySystem, human, "Lungs");
            var lungEffect = new ModifyLungGas { Ratios = new Dictionary<Gas, float> { [Gas.Oxygen] = 1f } };

            var modEv = new GetOrganMetabolismScaleModifierEvent(insertedLungs, lungEffect) { Scale = 1f };
            entityManager.EventBus.RaiseLocalEvent(human, ref modEv);
            Assert.That(modEv.Scale, Is.EqualTo(1.2f).Within(0.001f), "T2 cyber lungs should scale ModifyLungGas by effectiveness");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyberOrgan_CyberEyesT2_FlashDurationReduction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var head = GetHead(entityManager, human);
            var eyes = bodySystem.GetAllOrgans(human).First(o =>
                entityManager.TryGetComponent(o, out OrganComponent? comp) && comp.Category?.Id == "Eyes");

            var removeEv = new OrganRemoveRequestEvent(eyes);
            entityManager.EventBus.RaiseLocalEvent(eyes, ref removeEv);
            Assert.That(removeEv.Success, Is.True);

            var cyberEyes = entityManager.SpawnEntity("OrganCyberEyesT2", entityManager.GetComponent<TransformComponent>(human).Coordinates);
            var insertEv = new OrganInsertRequestEvent(head, cyberEyes);
            entityManager.EventBus.RaiseLocalEvent(head, ref insertEv);
            Assert.That(insertEv.Success, Is.True);

            var reductionEv = new GetFlashDurationReductionEvent();
            entityManager.EventBus.RaiseLocalEvent(human, ref reductionEv);
            Assert.That(reductionEv.Reduction, Is.EqualTo(TimeSpan.FromSeconds(10)),
                "T2+ cyber eyes should add 10s flash duration reduction");
        });

        await pair.CleanReturnAsync();
    }
}
