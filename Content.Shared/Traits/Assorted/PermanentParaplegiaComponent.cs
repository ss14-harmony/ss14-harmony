using Robust.Shared.GameStates;

namespace Content.Shared.Traits.Assorted;

/// <summary>
/// Character trait marker for paraplegia. Examine and cloning use this; foot organs carry
/// <c>FootTraitParaplegicComponent</c> for mechanical effects.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PermanentParaplegiaComponent : Component
{
}
