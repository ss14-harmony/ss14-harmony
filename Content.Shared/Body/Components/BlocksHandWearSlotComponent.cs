using Robust.Shared.GameStates;

namespace Content.Shared.Body.Components;

/// <summary>
/// Marker on a hand organ. The organ does not count toward allowing the gloves inventory slot
/// on the body it is attached to.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BlocksHandWearSlotComponent : Component
{
}
