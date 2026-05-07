using Robust.Shared.GameStates;

namespace Content.Shared.Eye.Blinding.Components;

/// <summary>
/// Marks an implanted eye organ as carrying blindness from character traits / cloning.
/// This is data-only — examine and flash use permanent blindness on the mob entity, not on the organ.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganTraitBlindnessComponent : Component
{
    /// <summary>
    /// Same scale as PermanentBlindnessComponent: 0 = maximum trait blindness floor, nonzero = blurred vision tier.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Blindness = 0;
}
