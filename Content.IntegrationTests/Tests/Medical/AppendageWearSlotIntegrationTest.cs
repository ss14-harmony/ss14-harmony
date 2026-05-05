using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Gloves and shoes are dropped / blocked when no eligible hand or foot organs are attached.
/// </summary>
[TestFixture]
[TestOf(typeof(AppendageWearSlotSystem))]
public sealed class AppendageWearSlotIntegrationTest : InteractionTest
{
    /// <summary>
    /// Humanoid with no leg/foot organs at spawn (MapInit never had feet) — used to assert glove/shoe rules reconcile from anatomy alone.
    /// </summary>
    [TestPrototypes]
    private const string AppendageWearTestPrototypes = @"
- type: entity
  id: IntegrationTestMobHumanNoLegOrgans
  parent: MobHuman
  categories: [ HideSpawnMenu ]
  components:
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: OrganHumanTorso
        - id: OrganHumanHead
        - id: OrganHumanArmLeft
        - id: OrganHumanArmRight
";

    /// <remarks>
    /// Match other medical InteractionTests — avoids odd client/player mob mismatch logs during pooled runs.
    /// </remarks>
    protected override string PlayerPrototype => "MobHuman";

    private static EntityUid GetBodyPart(IEntityManager entMan, EntityUid body, string category)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entMan.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0), $"Body should have {category}");
        return ev.Parts[0];
    }

    private static readonly ProtoId<OrganCategoryPrototype>[] HandCats =
        [new("HandLeft"), new("HandRight")];

    private static readonly ProtoId<OrganCategoryPrototype>[] FootCats =
        [new("FootLeft"), new("FootRight")];

    private static bool MatchesAnyCategory(OrganComponent oc, ProtoId<OrganCategoryPrototype>[] set)
    {
        if (oc.Category is not { } cat)
            return false;
        foreach (var id in set)
        {
            if (id == cat)
                return true;
        }

        return false;
    }

    private static int CountOrgansMatching(
        BodySystem bodySys,
        IEntityManager entMan,
        EntityUid patient,
        ProtoId<OrganCategoryPrototype>[] categories)
    {
        var n = 0;
        foreach (var organ in bodySys.GetAllOrgans(patient))
        {
            if (!entMan.TryGetComponent<OrganComponent>(organ, out var oc) || !MatchesAnyCategory(oc, categories))
                continue;
            n++;
        }

        return n;
    }

    /// <summary>
    /// Detach every organ matching <paramref name="categories"/> from the patient and drop it on the floor.
    /// The <see cref="OrganRemoveRequestEvent.Destination"/> mirrors what real surgery sets — without it,
    /// <see cref="SharedContainerSystem.Remove"/> would re-attach the organ into its grandparent container
    /// (e.g. a removed hand would jump from the arm's <c>limb_organs</c> back into the body's <c>body_organs</c>).
    /// </summary>
    private static void DetachOrgans(
        IEntityManager entMan,
        EntityUid patient,
        ProtoId<OrganCategoryPrototype>[] categories,
        string label)
    {
        var bodySys = entMan.System<BodySystem>();
        var dropAt = entMan.GetComponent<TransformComponent>(patient).Coordinates;
        var before = CountOrgansMatching(bodySys, entMan, patient, categories);
        foreach (var organ in bodySys.GetAllOrgans(patient).ToArray())
        {
            if (!entMan.TryGetComponent<OrganComponent>(organ, out var oc) || !MatchesAnyCategory(oc, categories))
                continue;

            var ev = new OrganRemoveRequestEvent(organ) { Destination = dropAt };
            entMan.EventBus.RaiseLocalEvent(organ, ref ev);
            Assert.That(ev.Success, Is.True, $"{label} organ removal should succeed ({organ})");
        }

        if (before > 0)
        {
            var after = CountOrgansMatching(bodySys, entMan, patient, categories);
            Assert.That(after, Is.EqualTo(0),
                $"Expected all {label.ToLowerInvariant()}-category organs detached (had {before}, still {after}).");
        }
    }

    private static void RemoveHands(IEntityManager entMan, EntityUid patient)
        => DetachOrgans(entMan, patient, HandCats, "Hand");

    private static void RemoveFeet(IEntityManager entMan, EntityUid patient)
        => DetachOrgans(entMan, patient, FootCats, "Foot");

    [Test]
    public async Task RemovingBothHands_DropsEquippedGloves()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;

        NetEntity glovesNet = default;

        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetCoordinates(TargetCoords);
            var gloves = SEntMan.SpawnEntity("ClothingHandsGlovesColorBlack", coords);
            glovesNet = SEntMan.GetNetEntity(gloves);
            var inv = SEntMan.System<InventorySystem>();
            Assert.That(inv.TryEquip(patient, gloves, "gloves", force: true), Is.True);

            RemoveHands(SEntMan, patient);
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var appendage = SEntMan.System<AppendageWearSlotSystem>();
            Assert.That(appendage.HasEligibleHandOrgan(patient), Is.False);

            var inv = SEntMan.System<InventorySystem>();
            Assert.That(inv.TryGetSlotEntity(patient, "gloves", out _), Is.False);

            var gloves = SEntMan.GetEntity(glovesNet);
            Assert.That(SEntMan.EntityExists(gloves), Is.True);
        });
    }

    [Test]
    public async Task NoHands_BlocksGloveEquip_WithReasonLocId()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;

        NetEntity glovesNet = default;

        await Server.WaitPost(() =>
        {
            RemoveHands(SEntMan, patient);
            var coords = SEntMan.GetCoordinates(TargetCoords);
            var gloves = SEntMan.SpawnEntity("ClothingHandsGlovesColorBlack", coords);
            glovesNet = SEntMan.GetNetEntity(gloves);

            var inv = SEntMan.System<InventorySystem>();
            Assert.That(inv.CanEquip(patient, gloves, "gloves", out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("appendage-wear-slot-gloves-no-hands"));
        });
        await RunTicks(1);
        _ = glovesNet;
    }

    [Test]
    public async Task ReinsertingOneHand_AllowsEquippingGlovesAgain()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;

        NetEntity glovesNet = default;

        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetCoordinates(TargetCoords);
            var gloves = SEntMan.SpawnEntity("ClothingHandsGlovesColorBlack", coords);
            glovesNet = SEntMan.GetNetEntity(gloves);

            var inv = SEntMan.System<InventorySystem>();
            Assert.That(inv.TryEquip(patient, gloves, "gloves", force: true), Is.True);

            RemoveHands(SEntMan, patient);
        });

        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            var armLeft = GetBodyPart(SEntMan, patient, "ArmLeft");
            var armBp = SEntMan.GetComponent<BodyPartComponent>(armLeft);
            var containerSys = SEntMan.System<SharedContainerSystem>();
            var patientCoords = SEntMan.GetComponent<TransformComponent>(patient).Coordinates;
            var hand = SEntMan.SpawnEntity("OrganHumanHandLeft", patientCoords);
            Assert.That(armBp.Organs, Is.Not.Null);
            Assert.That(containerSys.Insert(hand, armBp.Organs), Is.True);
        });

        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            var inv = SEntMan.System<InventorySystem>();
            var gloves = SEntMan.GetEntity(glovesNet);

            Assert.That(inv.CanEquip(patient, gloves, "gloves", out _), Is.True);
            Assert.That(inv.TryEquip(patient, gloves, "gloves", force: true), Is.True);
        });
    }

    [Test]
    public async Task RemovingBothFeet_DropsEquippedShoes()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;

        NetEntity shoesNet = default;

        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetCoordinates(TargetCoords);
            var shoes = SEntMan.SpawnEntity("ClothingShoesColorBlack", coords);
            shoesNet = SEntMan.GetNetEntity(shoes);
            var inv = SEntMan.System<InventorySystem>();
            Assert.That(inv.TryEquip(patient, shoes, "shoes", force: true), Is.True);

            RemoveFeet(SEntMan, patient);
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var inv = SEntMan.System<InventorySystem>();
            Assert.That(inv.TryGetSlotEntity(patient, "shoes", out _), Is.False);

            var shoes = SEntMan.GetEntity(shoesNet);
            Assert.That(SEntMan.EntityExists(shoes), Is.True);
        });
    }

    [Test]
    public async Task NoFeet_BlocksShoeEquip_WithReasonLocId()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;

        NetEntity shoesNet = default;

        await Server.WaitPost(() =>
        {
            RemoveFeet(SEntMan, patient);
            var coords = SEntMan.GetCoordinates(TargetCoords);
            var shoes = SEntMan.SpawnEntity("ClothingShoesColorBlack", coords);
            shoesNet = SEntMan.GetNetEntity(shoes);

            var inv = SEntMan.System<InventorySystem>();
            Assert.That(inv.CanEquip(patient, shoes, "shoes", out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("appendage-wear-slot-shoes-no-feet"));
        });
        await RunTicks(1);
        _ = shoesNet;
    }

    [Test]
    public async Task Diona_HasNoShoesSlot_FootOrgansHaveBlockerComponent()
    {
        await SpawnTarget("MobDiona");
        var diona = STarget!.Value;

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var inv = SEntMan.System<InventorySystem>();
            Assert.That(inv.HasSlot(diona, "shoes"), Is.False);

            var bodySys = SEntMan.System<BodySystem>();
            var sawFootLeft = false;
            var sawFootRight = false;
            foreach (var organ in bodySys.GetAllOrgans(diona))
            {
                if (!SEntMan.TryGetComponent<OrganComponent>(organ, out var oc) || oc.Category is not { } cat)
                    continue;

                if (cat == new ProtoId<OrganCategoryPrototype>("FootLeft"))
                {
                    Assert.That(SEntMan.HasComponent<BlocksFootWearSlotComponent>(organ), Is.True);
                    sawFootLeft = true;
                }
                else if (cat == new ProtoId<OrganCategoryPrototype>("FootRight"))
                {
                    Assert.That(SEntMan.HasComponent<BlocksFootWearSlotComponent>(organ), Is.True);
                    sawFootRight = true;
                }
            }

            Assert.That(sawFootLeft, Is.True, "Diona should have a FootLeft organ for this regression check.");
            Assert.That(sawFootRight, Is.True, "Diona should have a FootRight organ for this regression check.");
        });
    }

    [Test]
    public async Task MapInit_BodyWithoutFeet_BlocksShoeEquip_WithReasonLocId()
    {
        await SpawnTarget("IntegrationTestMobHumanNoLegOrgans");
        var patient = STarget!.Value;

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var appendage = SEntMan.System<AppendageWearSlotSystem>();
            Assert.That(appendage.HasEligibleFootOrgan(patient), Is.False);

            var coords = SEntMan.GetCoordinates(TargetCoords);
            var shoes = SEntMan.SpawnEntity("ClothingShoesColorBlack", coords);
            var inv = SEntMan.System<InventorySystem>();
            Assert.That(inv.CanEquip(patient, shoes, "shoes", out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("appendage-wear-slot-shoes-no-feet"));
        });
    }

    [Test]
    public async Task RecomputeAppendageWearSlots_IsIdempotent_WithEmptySlots()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;

        await Server.WaitAssertion(() =>
        {
            RemoveHands(SEntMan, patient);
            var appendage = SEntMan.System<AppendageWearSlotSystem>();
            appendage.RecomputeAppendageWearSlots(patient);
            appendage.RecomputeAppendageWearSlots(patient);

            var inv = SEntMan.System<InventorySystem>();
            Assert.That(inv.TryGetSlotEntity(patient, "gloves", out _), Is.False);
        });
    }
}
