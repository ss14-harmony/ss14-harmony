using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Prototypes;

/// <summary>
/// Defines the ordered surgical procedures for a body part of a given species.
/// One per (SpeciesId, OrganCategory) - e.g. HumanArmLeft, DionaLegRight.
/// </summary>
[Prototype]
public sealed partial class BodyPartSurgeryStepsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Species this config applies to (e.g. Human, Diona).
    /// </summary>
    [DataField("species", required: true)]
    public ProtoId<SpeciesPrototype> SpeciesId { get; private set; }

    /// <summary>
    /// Organ category (e.g. Torso, ArmLeft, LegRight). Uses OrganCategoryPrototype.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<OrganCategoryPrototype> OrganCategory { get; private set; }

    /// <summary>
    /// Ordered list of skin opening procedure IDs (last one gates tissue layer).
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> SkinOpenSteps { get; private set; } = new();

    /// <summary>
    /// Ordered list of skin closing procedure IDs (any done = skin closed).
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> SkinCloseSteps { get; private set; } = new();

    /// <summary>
    /// Ordered list of tissue opening procedure IDs (last one gates organ layer).
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> TissueOpenSteps { get; private set; } = new();

    /// <summary>
    /// Ordered list of tissue closing procedure IDs (any done = tissue closed).
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> TissueCloseSteps { get; private set; } = new();

    /// <summary>
    /// Organ procedure IDs for limbs (DetachLimb, AttachLimb). Organs use OrganSurgeryProceduresComponent.
    /// Hands and feet omit DetachLimb; only ArmLeft, ArmRight, LegLeft, LegRight include it.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> OrganSteps { get; private set; } = new();

    /// <summary>
    /// Deprecated. Use SkinOpenSteps and SkinCloseSteps. Kept for migration.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> SkinSteps { get; private set; } = new();

    /// <summary>
    /// Deprecated. Use TissueOpenSteps and TissueCloseSteps. Kept for migration.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> TissueSteps { get; private set; } = new();

    /// <summary>
    /// Resolves skin open/close steps, using new schema or deriving from deprecated SkinSteps.
    /// </summary>
    public IReadOnlyList<string> GetSkinOpenStepIds(IPrototypeManager prototypes)
    {
        if (SkinOpenSteps.Count > 0)
            return SkinOpenSteps.Select(s => s.ToString()).ToList();
        return DeriveOpenSteps(SkinSteps, prototypes);
    }

    /// <summary>
    /// Resolves skin close steps.
    /// </summary>
    public IReadOnlyList<string> GetSkinCloseStepIds(IPrototypeManager prototypes)
    {
        if (SkinCloseSteps.Count > 0)
            return SkinCloseSteps.Select(s => s.ToString()).ToList();
        return DeriveCloseSteps(SkinSteps, prototypes);
    }

    /// <summary>
    /// Resolves tissue open/close steps.
    /// </summary>
    public IReadOnlyList<string> GetTissueOpenStepIds(IPrototypeManager prototypes)
    {
        if (TissueOpenSteps.Count > 0)
            return TissueOpenSteps.Select(s => s.ToString()).ToList();
        return DeriveOpenSteps(TissueSteps, prototypes);
    }

    public IReadOnlyList<string> GetTissueCloseStepIds(IPrototypeManager prototypes)
    {
        if (TissueCloseSteps.Count > 0)
            return TissueCloseSteps.Select(s => s.ToString()).ToList();
        return DeriveCloseSteps(TissueSteps, prototypes);
    }

    private static List<string> DeriveOpenSteps(List<ProtoId<SurgeryProcedurePrototype>> steps, IPrototypeManager prototypes)
    {
        var result = new List<string>();
        foreach (var stepId in steps)
        {
            if (prototypes.TryIndex(stepId, out SurgeryProcedurePrototype? step) && step.HealAmount is { } h && !h.Empty)
                break;
            result.Add(stepId.ToString());
        }
        return result;
    }

    private static List<string> DeriveCloseSteps(List<ProtoId<SurgeryProcedurePrototype>> steps, IPrototypeManager prototypes)
    {
        var result = new List<string>();
        var inClose = false;
        foreach (var stepId in steps)
        {
            if (prototypes.TryIndex(stepId, out SurgeryProcedurePrototype? step) && step.HealAmount is { } h && !h.Empty)
                inClose = true;
            if (inClose)
                result.Add(stepId.ToString());
        }
        return result;
    }
}
