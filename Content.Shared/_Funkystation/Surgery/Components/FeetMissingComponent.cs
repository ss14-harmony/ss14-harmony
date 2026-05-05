using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Applied when the body has no functional feet (missing or trait-paraplegic on all feet).
/// Forces crawling state (knocked down), similar to <see cref="LegsMissingComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(LimbDetachmentEffectsSystem))]
public sealed partial class FeetMissingComponent : Component
{
}
