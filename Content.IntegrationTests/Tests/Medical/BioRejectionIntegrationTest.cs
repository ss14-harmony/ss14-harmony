using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
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
public sealed class BioRejectionIntegrationTest
{
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
    public async Task BioRejection_AppliesDamage_WhenIntegrityExceedsCapacity()
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
        var mapData = await pair.CreateTestMap();

        const int excess = 1;
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

            Assert.That(entityManager.HasComponent<IntegrityUsageComponent>(patient), Is.True);
            Assert.That(entityManager.GetComponent<IntegrityUsageComponent>(patient).Usage, Is.EqualTo(1));
        });

        // Ramp rate is 0.1 per second per excess; with excess=1, ~10 seconds to reach target.
        // Run ~2.3 seconds (70 ticks) so damage ramps to ~0.2-0.3.
        const int tickCount = 70;
        await pair.RunTicksSync(tickCount);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.EntityExists(patient), Is.True, "Patient should still exist");
            Assert.That(entityManager.TryGetComponent(patient, out DamageableComponent? damageable), Is.True, "Patient should have DamageableComponent");

            var bioRejectionDamage = damageable!.Damage.DamageDict.TryGetValue("BioRejection", out var d) ? d : FixedPoint2.Zero;

            Assert.That(bioRejectionDamage, Is.GreaterThanOrEqualTo(FixedPoint2.New(0.1f)), "Bio-rejection damage should have ramped up from bio-rejection");
            Assert.That(bioRejectionDamage, Is.LessThanOrEqualTo(FixedPoint2.New(excess)), "Bio-rejection damage should not exceed target (excess)");
        });

        await pair.CleanReturnAsync();
    }
}
