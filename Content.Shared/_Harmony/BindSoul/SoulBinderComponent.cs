using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.BindSoul;

[RegisterComponent]
public sealed partial class SoulBinderComponent : Component
{
    [DataField]
    public EntityUid BindedItem;

    [DataField]
    public string? BinderPrototype = "MobSkeletonLich";

    [DataField]
    public float DeathCount;

    [DataField]
    public EntProtoId LinkBeamProto = "LichBeam";
}
