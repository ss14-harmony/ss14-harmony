using Robust.Shared.Prototypes;

namespace Content.Shared.Cybernetics.Components;

[RegisterComponent]
public sealed partial class CyberArmStorageActionComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionOpenCyberArmStorageLeft";

    [DataField]
    public EntityUid? ActionEntity;
}
