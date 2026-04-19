using Content.Shared.Movement.Events;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Applied when both legs are missing. Forces crawling state (knocked down).
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(LimbDetachmentEffectsSystem))]
public sealed partial class LegsMissingComponent : Component
{
}
