using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Xenograft;

/// <summary>
/// Marks a non-humanoid creature mob's donor species for surgery step resolution and UI preview.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CreatureDonorSpeciesComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<SpeciesPrototype> Species;

    /// <summary>
    /// Optional entity to spawn for the surgery diagram preview instead of species doll prototype.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? PreviewOverride;
}
