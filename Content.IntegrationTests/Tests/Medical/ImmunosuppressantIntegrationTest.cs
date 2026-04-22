using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Integrity.Events;
using Content.Shared.Medical.Surgery;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(BioRejectionSystem))]
public sealed class ImmunosuppressantIntegrationTest
{
    private static readonly ProtoId<ReagentPrototype> ImmunosuppressantPrototype = "Immunosuppressant";

    private static EntityUid GetTorso(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Torso") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static EntityUid GetHeart(IEntityManager entityManager, BodySystem bodySystem, EntityUid body)
    {
        return bodySystem.GetAllOrgans(body).First(o =>
            entityManager.TryGetComponent(o, out OrganComponent? comp) && comp.Category?.Id == "Heart");
    }

    [Test]
    public async Task Immunosuppressant_AppliesIntegrityImmunityBoost_WhenMetabolized()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false
        });
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var bloodstreamSystem = entityManager.System<SharedBloodstreamSystem>();
        var mapData = await pair.CreateTestMap();

        const int capacity = 6;
        EntityUid patient = default;

        await server.WaitPost(() =>
        {
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso = GetTorso(entityManager, patient);
            var naturalHeart = GetHeart(entityManager, bodySystem, patient);

            var removeEv = new OrganRemoveRequestEvent(naturalHeart);
            entityManager.EventBus.RaiseLocalEvent(naturalHeart, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove natural heart to free slot");

            var biosyntheticHeart = entityManager.SpawnEntity("OrganBiosyntheticHeart", entityManager.GetComponent<TransformComponent>(patient).Coordinates);
            var insertEv = new OrganInsertRequestEvent(torso, biosyntheticHeart);
            entityManager.EventBus.RaiseLocalEvent(torso, ref insertEv);
            Assert.That(insertEv.Success, Is.True, "Insert biosynthetic heart should succeed");

            var penaltyEv = new IntegrityPenaltyAppliedEvent(patient, capacity, "test", IntegrityPenaltyCategory.DirtyRoom);
            entityManager.EventBus.RaiseLocalEvent(patient, ref penaltyEv);
        });

        await pair.RunTicksSync(70);

        var damageBeforeImmunosuppressant = FixedPoint2.Zero;
        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.EntityExists(patient), Is.True);
            Assert.That(entityManager.TryGetComponent(patient, out DamageableComponent? damageable), Is.True);
            damageBeforeImmunosuppressant = damageable!.Damage.DamageDict.TryGetValue("BioRejection", out var d) ? d : FixedPoint2.Zero;
            Assert.That(damageBeforeImmunosuppressant, Is.GreaterThanOrEqualTo(FixedPoint2.New(0.1f)), "Bio-rejection damage should have ramped up before immunosuppressant");
        });

        await server.WaitPost(() =>
        {
            var solution = new Solution();
            solution.AddReagent(new ReagentId(ImmunosuppressantPrototype, null), FixedPoint2.New(10));
            Assert.That(bloodstreamSystem.TryAddToBloodstream((patient, entityManager.GetComponent<BloodstreamComponent>(patient)), solution), Is.True, "Add Immunosuppressant to bloodstream");
        });

        // Run enough ticks for the Immunosuppressant to be metabolized at least once and apply the boost.
        await pair.RunTicksSync(600);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.EntityExists(patient), Is.True);

            // Per the recent patch, Immunosuppressant metabolism should add an IntegrityImmunityBoostComponent
            // with Amount == 10 to an organ on the patient (instead of directly modifying bio-rejection damage).
            Assert.That(bodySystem.TryGetOrgansWithComponent<IntegrityImmunityBoostComponent>(patient, out var boostOrgans), Is.True,
                "At least one organ should have IntegrityImmunityBoostComponent after Immunosuppressant metabolism");
            Assert.That(boostOrgans, Is.Not.Empty, "Boost organs list should not be empty");

            var boost = boostOrgans[0].Comp;
            Assert.That(boost.Amount, Is.EqualTo(10), "Immunosuppressant should apply an integrity boost of 10 (per the recent patch)");
            Assert.That(boost.ExpiresAt, Is.GreaterThan(TimeSpan.Zero), "Boost should have a non-zero expiration time set while Immunosuppressant is actively metabolizing");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BioRejection_DoesNotApply_ToEntitiesWithoutBloodstream()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false
        });
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        EntityUid skeleton = default;

        await server.WaitPost(() =>
        {
            skeleton = entityManager.SpawnEntity("MobSkeletonPerson", mapData.GridCoords);
            Assert.That(entityManager.HasComponent<BloodstreamComponent>(skeleton), Is.False, "Skeleton should not have BloodstreamComponent");
        });

        await pair.RunTicksSync(200);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.EntityExists(skeleton), Is.True);
            Assert.That(entityManager.TryGetComponent(skeleton, out DamageableComponent? damageable), Is.True);
            var bioRejectionDamage = damageable!.Damage.DamageDict.TryGetValue("BioRejection", out var d) ? d : FixedPoint2.Zero;
            Assert.That(bioRejectionDamage, Is.EqualTo(FixedPoint2.Zero), "Entities without BloodstreamComponent should not receive bio-rejection damage");
        });

        await pair.CleanReturnAsync();
    }
}
