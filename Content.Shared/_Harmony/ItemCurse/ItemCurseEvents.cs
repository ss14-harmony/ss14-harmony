using Content.Shared.Actions;

namespace Content.Shared.ItemCurse;

/// <summary>
/// Raised when using the ItemCurse action.
/// </summary>
[ByRefEvent]
public sealed partial class OnItemCurseActionEvent : InstantActionEvent;
