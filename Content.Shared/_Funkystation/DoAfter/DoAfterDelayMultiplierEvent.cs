namespace Content.Shared.DoAfter;

/// <summary>
/// Raised on a DoAfter's user before the DoAfter is started so other systems can scale its delay.
/// The final delay is <c>args.Delay * Multiplier</c>. Multiplier is clamped to be non-negative.
/// Subscribers should multiply <see cref="Multiplier"/> (e.g. <c>args.Multiplier *= 2f</c>) rather than assigning
/// so multiple slowdowns compound correctly.
/// </summary>
[ByRefEvent]
public struct DoAfterDelayMultiplierEvent
{
    public float Multiplier;

    public DoAfterDelayMultiplierEvent()
    {
        Multiplier = 1f;
    }
}
