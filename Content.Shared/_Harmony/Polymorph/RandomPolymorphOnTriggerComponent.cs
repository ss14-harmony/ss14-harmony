using Content.Shared.Polymorph;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.Polymorph;

/// <summary>
/// Polymorphs the enity when triggered.
/// If TargetUser is true it will polymorph the user instead.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RandomPolymorphOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// Polymorph settings.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<PolymorphPrototype>>? Polymorph;
}
