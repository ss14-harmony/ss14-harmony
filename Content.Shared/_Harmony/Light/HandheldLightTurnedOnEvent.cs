namespace Content.Shared._Harmony.Light;

/// <summary>
/// Raised when a handheld light is successfully turned on.
/// </summary>
[ByRefEvent]
public readonly record struct HandheldLightTurnedOnEvent(EntityUid User);
