using Robust.Shared.GameStates;

namespace Content.Shared.Traits.Assorted;

/// <summary>
/// This is used for the uncloneable trait.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UncloneableComponent : Component
{

    /// Harmony - New Uncloneable Trait
    /// <summary>
    /// Can this player be cloned using a cloning pod?
    /// </summary>
     [DataField, AutoNetworkedField]
     public bool Cloneable = false;

}
