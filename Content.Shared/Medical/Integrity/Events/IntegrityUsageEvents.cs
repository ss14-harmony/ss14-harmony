namespace Content.Shared.Medical.Integrity.Events;

/// <summary>
/// Raised to request the integrity cost of an organ. Response is set in <see cref="Cost"/>.
/// </summary>
[ByRefEvent]
public record struct IntegrityCostRequestEvent(EntityUid Organ)
{
    /// <summary>
    /// The integrity cost. Populated by IntegrityUsageSystem.
    /// </summary>
    public int Cost { get; set; }
}
