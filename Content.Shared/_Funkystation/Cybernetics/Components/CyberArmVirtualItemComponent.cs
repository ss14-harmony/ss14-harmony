using Robust.Shared.GameStates;

namespace Content.Shared.Cybernetics.Components;

/// <summary>
/// Marker component for virtual items spawned by cyber arm selection.
/// Used to distinguish cyber arm virtual items from other virtual items (e.g. pulling, wielding).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberArmVirtualItemComponent : Component
{
}
