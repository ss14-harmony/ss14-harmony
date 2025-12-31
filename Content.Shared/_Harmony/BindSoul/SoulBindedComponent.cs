using Robust.Shared.GameStates;

namespace Content.Shared._Harmony.BindSoul;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SoulBindedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Owner;
}
