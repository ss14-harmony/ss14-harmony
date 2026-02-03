using Content.Shared._Harmony.Bingle.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared._Harmony.Bingle.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedBinglePitSystem))]
[AutoGenerateComponentState]
public sealed partial class BingleSpawnerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Pit = EntityUid.Invalid;
}
