using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Tag;

namespace Content.Shared._Harmony.Tag;

/// <summary>
/// Adds a list of tags to the entity on mapinit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(TagSystem))]
public sealed partial class AddTagOnMapInitComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<TagPrototype>> Tags = new();
}
