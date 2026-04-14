using Content.Shared.Medical.Integrity;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Integrity.Components;

/// <summary>
/// Contextual integrity penalties on a body (dirty room, improper tools). Cleared when surgery is performed properly.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(IntegrityPenaltyAggregatorSystem))]
public sealed partial class IntegritySurgeryComponent : Component
{
    /// <summary>
    /// List of contextual penalties: (Reason, Category, Amount).
    /// </summary>
    [DataField]
    public List<IntegrityPenaltyEntry> Entries { get; set; } = new();
}

/// <summary>
/// A single contextual integrity penalty entry. May have nested children for hierarchical display (e.g. body part -> step -> improvised tool).
/// </summary>
public readonly record struct IntegrityPenaltyEntry(string Reason, IntegrityPenaltyCategory Category, int Amount, List<IntegrityPenaltyEntry>? Children = null);
