namespace Content.Shared.Cybernetics.Events;

/// <summary>
/// Raised when cybernetics maintenance panel state changes or wire repair completes.
/// Notifies stats system to recalculate; notifies storage system when panel is closed or bolts loosened.
/// </summary>
[ByRefEvent]
public readonly record struct CyberMaintenanceStateChangedEvent(
    EntityUid Body,
    bool RepairCompleted = false,
    bool PanelClosed = false,
    bool BoltsLoosened = false);
