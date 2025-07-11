using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.BloodBounds.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class BloodBoundComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Bound;

    [DataField]
    public ProtoId<FactionIconPrototype> BloodBoundIcon = "BloodBoundFaction";

    public override bool SessionSpecific => true;
}
