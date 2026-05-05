using Robust.Shared.GameStates;

namespace Content.Shared.Eye.Blinding.Components;

/// <summary>
/// Marker: the body has no eye organs installed; vision is blocked until eyes are reinserted.
/// </summary>
[NetworkedComponent, RegisterComponent]
public sealed partial class EyelessBlindnessComponent : Component
{
}
