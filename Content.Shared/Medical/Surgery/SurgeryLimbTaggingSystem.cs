using Content.Shared.Body;
using Content.Shared.Body.Components;
// using Content.Shared.Cybernetics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Preferences;
using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Tags body parts with species and organ category when inserted into body_organs.
/// Covers both initial spawn (EntityTableContainerFill) and mid-round limb attachment.
/// </summary>
public sealed class SurgeryLimbTaggingSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    // private static readonly IReadOnlyDictionary<string, string> CyberLimbStepsConfigIds = new Dictionary<string, string>
    // {
    //     ["ArmLeft"] = "CyberLimbArmLeft",
    //     ["ArmRight"] = "CyberLimbArmRight",
    //     ["LegLeft"] = "CyberLimbLegLeft",
    //     ["LegRight"] = "CyberLimbLegRight",
    // };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyPartComponent, EntGotInsertedIntoContainerMessage>(OnBodyPartInserted);
    }

    private void OnBodyPartInserted(Entity<BodyPartComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        var body = args.Container.Owner;
        if (!Exists(body))
            return;

        if (!TryComp<OrganComponent>(ent, out var organ) || organ.Category is not { } category)
            return;

        var hadComp = HasComp<SurgeryBodyPartComponent>(ent);
        var comp = EnsureComp<SurgeryBodyPartComponent>(ent);
        var resolvedSpecies = ResolveSpecies(body, ent, category);
        // Preserve limb's original species when grafting (e.g. vox arm onto human) so surgery steps
        // and UI resolve correctly. Always update when: fresh spawn, invalid species, or grafting
        // (limb's prototype species differs from body - ensures DetachLimb/InsertOrgan show for grafted limbs).
        var bodySpecies = TryComp<HumanoidProfileComponent>(body, out var profile) ? profile.Species : default;
        var isGraft = bodySpecies != default && resolvedSpecies != bodySpecies;
        if (!hadComp || !_prototypes.TryIndex(comp.SpeciesId, out _) || isGraft)
        {
            comp.SpeciesId = resolvedSpecies;
        }
        comp.OrganCategory = category;
        // if (HasComp<CyberLimbComponent>(ent) && CyberLimbStepsConfigIds.TryGetValue(category.ToString(), out var stepsConfigId))
        //     comp.StepsConfigId = new ProtoId<BodyPartSurgeryStepsPrototype>(stepsConfigId);
        Dirty(ent, comp);

        // Grafted limbs must have SurgeryLayerComponent for health analyzer to show DetachLimb/InsertOrgan.
        // BodyPartComponent gets it from BodySystem.OnBodyPartInit, but ensure it exists for any limb inserted into body.
        EnsureComp<SurgeryLayerComponent>(ent);
    }

    /// <summary>
    /// Tags a limb organ (arm, leg, hand, foot) with SurgeryBodyPartComponent and SurgeryLayerComponent.
    /// Called by LimbDetachmentEffectsSystem for InitialBody species whose limbs lack BodyPartComponent.
    /// </summary>
    public void TagLimbOrgan(EntityUid limb, EntityUid body, ProtoId<OrganCategoryPrototype> category)
    {
        if (HasComp<BodyPartComponent>(limb))
            return;

        var speciesId = ResolveSpecies(body, limb);
        var comp = EnsureComp<SurgeryBodyPartComponent>(limb);
        comp.SpeciesId = speciesId;
        comp.OrganCategory = category;
        Dirty(limb, comp);
        EnsureComp<SurgeryLayerComponent>(limb);
    }

    private ProtoId<Humanoid.Prototypes.SpeciesPrototype> ResolveSpecies(EntityUid body, EntityUid limb, ProtoId<OrganCategoryPrototype>? category = null)
    {
        // For grafted limbs (e.g. vox arm on human), derive species from prototype if body's species
        // would give wrong/no config. Organ IDs follow Organ{Species}{Category} e.g. OrganVoxArmLeft.
        if (category.HasValue && TryComp<MetaDataComponent>(limb, out var meta) && meta.EntityPrototype is { } proto)
        {
            var id = proto.ID;
            if (id.StartsWith("Organ", StringComparison.OrdinalIgnoreCase) && id.Length > 5)
            {
                var suffix = id[5..]; // e.g. "VoxArmLeft"
                if (_prototypes.TryIndex<BodyPartSurgeryStepsPrototype>(suffix, out var stepsConfig)
                    && stepsConfig.OrganCategory == category.Value)
                {
                    return stepsConfig.SpeciesId;
                }
            }
        }

        if (TryComp<HumanoidProfileComponent>(body, out var profile))
            return profile.Species;

        return Content.Shared.Preferences.HumanoidCharacterProfile.DefaultSpecies;
    }
}
