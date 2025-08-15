using Robust.Shared.GameStates;

namespace Content.Shared._Harmony.ItemCurse;

/// <summary>
/// Component used as a marker for an item marked by the ItemCurse ability.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedItemCurseSystem))]
public sealed partial class CurseMarkerComponent : Component
{
    /// <summary>
    /// The action that marked this item.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? MarkedByAction;
}
