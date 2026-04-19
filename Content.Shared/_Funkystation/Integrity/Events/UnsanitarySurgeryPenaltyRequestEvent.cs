using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Prototypes;

namespace Content.Shared.Medical.Integrity.Events;

/// <summary>
/// Raised when a surgery step completes. Server handler computes unsanitary and improvised-tool penalties,
/// clears previous entries, and applies new ones.
/// </summary>
[ByRefEvent]
public record struct UnsanitarySurgeryPenaltyRequestEvent(
    EntityUid Body,
    EntityUid BodyPart,
    string StepId,
    SurgeryLayer Layer,
    bool IsImprovised,
    SurgeryStepPrototype? Step,
    SurgeryProcedurePrototype? Procedure);
