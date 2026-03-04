using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Events;

/// <summary>
/// Raised on a body part when a surgery step's DoAfter has completed.
/// Body raises this; BodyPart handlers apply the step (add to performed list, raise organ/limb events).
/// </summary>
[ByRefEvent]
public record struct SurgeryStepCompletedEvent(
    EntityUid User,
    EntityUid Target,
    EntityUid BodyPart,
    ProtoId<SurgeryProcedurePrototype> ProcedureId,
    SurgeryLayer Layer,
    EntityUid? Organ,
    SurgeryStepPrototype? Step,
    SurgeryProcedurePrototype? Procedure)
{
    /// <summary>
    /// Set to true if the step was applied (e.g. added to performed list).
    /// Handlers should set this to prevent duplicate application.
    /// </summary>
    public bool Handled;
}
