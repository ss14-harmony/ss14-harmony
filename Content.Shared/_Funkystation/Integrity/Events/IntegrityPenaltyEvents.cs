using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Integrity.Components;

namespace Content.Shared.Medical.Integrity.Events;

/// <summary>
/// Raised when a surgery procedure applies an integrity penalty to a body part (organ, limb, implant).
/// </summary>
[ByRefEvent]
public readonly record struct SurgeryPenaltyAppliedEvent(EntityUid BodyPart, int Amount);

/// <summary>
/// Raised when a surgery procedure removes an integrity penalty from a body part.
/// </summary>
[ByRefEvent]
public readonly record struct SurgeryPenaltyRemovedEvent(EntityUid BodyPart, int Amount);

/// <summary>
/// Raised to add a contextual integrity penalty to a body (dirty room, improper tools, unsanitary surgery).
/// </summary>
[ByRefEvent]
public readonly record struct IntegrityPenaltyAppliedEvent(EntityUid Body, int Amount, string Reason, IntegrityPenaltyCategory Category, List<IntegrityPenaltyEntry>? Children = null);

/// <summary>
/// Raised to clear contextual integrity penalties by category.
/// </summary>
[ByRefEvent]
public readonly record struct IntegrityPenaltyClearedEvent(EntityUid Body, IntegrityPenaltyCategory Category);

/// <summary>
/// Raised to request the total integrity penalty for a body. Response is set in <see cref="Total"/>.
/// </summary>
[ByRefEvent]
public record struct IntegrityPenaltyTotalRequestEvent(EntityUid Body)
{
    /// <summary>
    /// The total integrity penalty. Populated by IntegrityPenaltyAggregatorSystem.
    /// </summary>
    public int Total { get; set; }
}
