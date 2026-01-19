using Content.Shared._Harmony.Bingle.EntitySystems;
using Content.Shared.Polymorph;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.Bingle.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedBinglePitSystem))]
[AutoGenerateComponentState]
public sealed partial class BingleComponent : Component
{
    /// <summary>
    /// Whether this bingle has been upgraded before.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Upgraded;

    /// <summary>
    /// The polymorph that will be given to the entity whenever it is able to upgrade.
    /// </summary>
    [DataField]
    public ProtoId<PolymorphPrototype> UpgradePolymorph = "BinglePolymorph";
}
