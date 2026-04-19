using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Medical.LimbRegeneration;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Integration test for slime limb regeneration.
/// Amputates a slime's leg via OrganRemoveRequestEvent (bypasses surgery UI),
/// waits for the regeneration delay, then verifies the leg is restored.
/// </summary>
[TestFixture]
[TestOf(typeof(LimbRegenerationSystem))]
public sealed class SlimeLimbRegenerationIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static EntityUid GetLeg(IEntityManager entityManager, EntityUid body, string category = "LegLeft")
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0), $"Body should have a {category}");
        return ev.Parts[0];
    }

    [Test]
    public async Task SlimePerson_AmputateLeg_RegeneratesAfterDelay()
    {
        await SpawnTarget("MobSlimePerson");
        var patient = STarget!.Value;

        EntityUid leg = default;
        await Server.WaitPost(() =>
        {
            leg = GetLeg(SEntMan, patient);
            var coords = SEntMan.GetComponent<Robust.Shared.GameObjects.TransformComponent>(patient).Coordinates;
            var removeEv = new OrganRemoveRequestEvent(leg) { Destination = coords };
            SEntMan.EventBus.RaiseLocalEvent(leg, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "OrganRemoveRequestEvent should succeed");
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(leg), Is.True, "Leg entity should exist after removal");
            Assert.That(SEntMan.TryGetComponent(leg, out BodyPartComponent? legBodyPart), Is.True);
            Assert.That(legBodyPart!.Body, Is.Null, "Leg should no longer be attached to body after removal");
        });

        await Server.WaitPost(() =>
        {
            var regen = SEntMan.EnsureComponent<Content.Server.Medical.LimbRegeneration.Components.SlimeLimbRegenerationComponent>(patient);
            regen.RegenerationDelay = TimeSpan.FromSeconds(5);
            // SlimeLimbRegenerationComponent is server-only (no NetworkedComponentAttribute) - do not call Dirty
        });

        // Advance time by 8 seconds (5 s delay + 3 s buffer). At 60 ticks/sec = 480 ticks.
        await RunTicks(480);

        await Server.WaitAssertion(() =>
        {
            var ev = new BodyPartQueryByTypeEvent(patient) { Category = new ProtoId<OrganCategoryPrototype>("LegLeft") };
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Parts, Has.Count.GreaterThan(0), "Slime should have regenerated a left leg");
            var newLeg = ev.Parts[0];
            Assert.That(SEntMan.TryGetComponent(newLeg, out BodyPartComponent? bodyPartComp), Is.True);
            Assert.That(bodyPartComp!.Body, Is.EqualTo(patient), "Regenerated leg should be attached to slime body");
        });
    }
}
