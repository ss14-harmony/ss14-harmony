namespace Content.Shared.Medical.Surgery.Events;

/// <summary>
/// Raised on a body when a surgery step has completed and the Health Analyzer UI should refresh.
/// Used instead of subscribing to SurgeryStepCompletedEvent to avoid duplicate subscription conflicts.
/// </summary>
[ByRefEvent]
public record struct SurgeryUiRefreshRequestEvent;
