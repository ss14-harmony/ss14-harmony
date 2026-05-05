using Robust.Shared.GameStates;

namespace Content.Shared.Body.Components;

/// <summary>
/// Marker on a foot organ. The organ does not count toward allowing the footwear inventory slot
/// (e.g. shoes) on the body it is attached to.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BlocksFootWearSlotComponent : Component
{
}
