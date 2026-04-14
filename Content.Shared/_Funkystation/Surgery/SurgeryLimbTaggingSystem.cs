using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Tags body parts with species and organ category when inserted into body_organs.
/// Covers both initial spawn (EntityTableContainerFill) and mid-round limb attachment.
/// </summary>
public sealed class SurgeryLimbTaggingSystem : EntitySystem
{
    [Dependency] private readonly SurgeryLayerSystem _surgeryLayer = default!;

    private static readonly IReadOnlyDictionary<string, string> CyberLimbStepsConfigIds = new Dictionary<string, string>
    {
        ["ArmLeft"] = "CyberLimbArmLeft",
        ["ArmRight"] = "CyberLimbArmRight",
        ["LegLeft"] = "CyberLimbLegLeft",
        ["LegRight"] = "CyberLimbLegRight",
    };

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

        var speciesId = ResolveSpecies(body, ent);
        var comp = EnsureComp<SurgeryBodyPartComponent>(ent);
        comp.SpeciesId = speciesId;
        comp.OrganCategory = category;
        if (HasComp<CyberLimbComponent>(ent) && CyberLimbStepsConfigIds.TryGetValue(category.ToString(), out var stepsConfigId))
            comp.StepsConfigId = new ProtoId<BodyPartSurgeryStepsPrototype>(stepsConfigId);
        else
        {
            var stepsConfig = _surgeryLayer.GetStepsConfig(speciesId, category);
            if (stepsConfig != null)
                comp.StepsConfigId = new ProtoId<BodyPartSurgeryStepsPrototype>(stepsConfig.ID);
        }
        Dirty(ent, comp);
    }

    private ProtoId<Humanoid.Prototypes.SpeciesPrototype> ResolveSpecies(EntityUid body, EntityUid limb)
    {
        if (TryComp<HumanoidProfileComponent>(body, out var humanoidProfile))
            return humanoidProfile.Species;

        return HumanoidCharacterProfile.DefaultSpecies;
    }
}
