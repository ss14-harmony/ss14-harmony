#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Verifies that every playable species' body parts declare organ slots covering the organs
/// they spawn with. The slots list is used by <c>BodyPartOrganSystem</c> and <c>SurgerySystem</c>
/// to enforce organ category matching and duplicate prevention during insertion. When a body part
/// spawns with organs but its <c>BodyPartComponent.Slots</c> is empty, validation is bypassed —
/// which is why repeated organ removal/re-insertion surgery broke for non-human species (humans
/// were the only species with slots declared).
/// </summary>
[TestFixture]
public sealed class SpeciesBodyPartSlotsIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static IEnumerable<string> HumanoidMobs()
    {
        yield return "MobHuman";
        yield return "MobVox";
        yield return "MobReptilian";
        yield return "MobMoth";
        yield return "MobDiona";
        yield return "MobVulpkanin";
        yield return "MobArachnid";
        yield return "MobSkeletonPerson";
        yield return "MobSlimePerson";
        yield return "MobDwarf";
    }

    /// <summary>
    /// For every body part of the given mob, every child organ's category must appear in the part's
    /// Slots. Otherwise insertion validation is skipped for that body part, producing the
    /// "surgery breaks after organ reinsertion" regression for non-human species.
    /// </summary>
    [Test]
    [TestCaseSource(nameof(HumanoidMobs))]
    public async Task EveryBodyPart_SlotsCoverChildOrganCategories(string mobPrototype)
    {
        await SpawnTarget(mobPrototype);
        var patient = STarget!.Value;

        await Server.WaitAssertion(() =>
        {
            var query = new BodyPartQueryEvent(patient);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref query);
            Assert.That(query.Parts, Is.Not.Empty, $"{mobPrototype} should have body parts");

            foreach (var bodyPart in query.Parts)
            {
                if (!SEntMan.TryGetComponent(bodyPart, out BodyPartComponent? bodyPartComp))
                    continue;
                if (bodyPartComp.Organs == null)
                    continue;

                var partCategory = SEntMan.TryGetComponent(bodyPart, out OrganComponent? partOrgan)
                    ? partOrgan.Category?.Id ?? "?"
                    : "?";

                var childOrganCategories = bodyPartComp.Organs.ContainedEntities
                    .Select(o => SEntMan.TryGetComponent(o, out OrganComponent? oc) ? oc.Category : null)
                    .Where(c => c.HasValue)
                    .Select(c => c!.Value)
                    .ToList();

                if (childOrganCategories.Count == 0)
                    continue;

                Assert.That(bodyPartComp.Slots, Is.Not.Empty,
                    $"{mobPrototype} body part '{partCategory}' spawns with organs ({string.Join(", ", childOrganCategories.Select(c => c.Id))}) " +
                    "but declares no Slots. Organ insertion validation is skipped when Slots is empty, " +
                    "which breaks repeated organ removal/re-insertion surgery for this species.");

                foreach (var childCategory in childOrganCategories)
                {
                    Assert.That(bodyPartComp.Slots, Does.Contain(childCategory),
                        $"{mobPrototype} body part '{partCategory}' hosts organ category '{childCategory}' but does not list it in Slots.");
                }
            }
        });
    }

    /// <summary>
    /// Hands and feet live inside arm and leg body parts. Their parent limb must declare the
    /// corresponding slot so that transplanted limbs can receive hands and feet via surgery.
    /// </summary>
    [Test]
    [TestCaseSource(nameof(HumanoidMobs))]
    public async Task LimbBodyParts_DeclareHandOrFootSlot(string mobPrototype)
    {
        await SpawnTarget(mobPrototype);
        var patient = STarget!.Value;

        await Server.WaitAssertion(() =>
        {
            AssertLimbSlot(patient, "ArmLeft", "HandLeft");
            AssertLimbSlot(patient, "ArmRight", "HandRight");
            AssertLimbSlot(patient, "LegLeft", "FootLeft");
            AssertLimbSlot(patient, "LegRight", "FootRight");
        });
    }

    private void AssertLimbSlot(EntityUid patient, string limbCategory, string expectedChildSlot)
    {
        var ev = new BodyPartQueryByTypeEvent(patient) { Category = new ProtoId<OrganCategoryPrototype>(limbCategory) };
        SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
        if (ev.Parts.Count == 0)
            return;

        var bodyPart = ev.Parts[0];
        Assert.That(SEntMan.TryGetComponent(bodyPart, out BodyPartComponent? bodyPartComp), Is.True,
            $"{limbCategory} should have BodyPartComponent");
        Assert.That(bodyPartComp!.Slots.Select(s => s.Id), Does.Contain(expectedChildSlot),
            $"{limbCategory} must declare Slots containing '{expectedChildSlot}' so hand/foot transplant surgery validates correctly.");
    }
}
