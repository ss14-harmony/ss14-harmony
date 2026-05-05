using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Prototypes;

/// <summary>
/// Reusable preset for limbs that skip skin/tissue layers (organ layer only).
/// Used by Skeleton arms/legs and Cyber limbs.
/// </summary>
[Prototype]
public sealed partial class BodyPartSurgeryStepsPresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Organ procedure IDs (e.g. DetachLimb, AttachLimb). Skin/tissue steps are implicitly empty.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> OrganSteps { get; private set; } = new();
}
