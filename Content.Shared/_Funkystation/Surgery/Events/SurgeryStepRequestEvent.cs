using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Events;

/// <summary>
/// Raised on a body part when surgery step validation is needed.
/// Body raises this before starting DoAfter; handlers set Valid/RejectReason.
/// </summary>
[ByRefEvent]
public record struct SurgeryStepRequestEvent(
    EntityUid User,
    EntityUid Target,
    ProtoId<SurgeryProcedurePrototype> ProcedureId,
    SurgeryLayer Layer,
    EntityUid? Organ,
    BodyPartSurgeryStepsPrototype? StepsConfig)
{
    public bool Valid = true;
    public string? RejectReason;
}
