using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Events;

/// <summary>
/// Raised when a surgery step is requested (e.g. from Health Analyzer BUI).
/// SurgerySystem validates and optionally starts DoAfter.
/// </summary>
[ByRefEvent]
public record struct SurgeryRequestEvent(
    EntityUid Analyzer,
    EntityUid User,
    EntityUid Target,
    EntityUid BodyPart,
    ProtoId<SurgeryProcedurePrototype> ProcedureId,
    SurgeryLayer Layer,
    bool IsImprovised,
    EntityUid? Organ = null)
{
    public bool Valid;
    public string? RejectReason;

    /// <summary>
    /// Set by SurgerySystem when valid and an improvised tool was used.
    /// </summary>
    public bool UsedImprovisedTool;

    /// <summary>
    /// The tool entity used when valid (for improvised popup name).
    /// </summary>
    public EntityUid? ToolUsed;
}
