using Content.Shared.Actions;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.BindSoul;

public sealed partial class OnBindSoulActionEvent : InstantActionEvent
{
    [DataField]
    public ProtoId<PolymorphPrototype> Polymorph = "LichPolymorph";

    [DataField]
    public EntityUid? BindedItem = null;

    [DataField]
    public EntityUid BindSoulAction;
}
