using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Tag;

/// <summary>
/// Adds a list of tags to the entity on mapinit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(TagSystem))]
public sealed partial class AddTagOnMapInitComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<TagPrototype>> Tags = new();
}
