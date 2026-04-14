using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Applied when one leg is missing. Reduces movement speed via RefreshMovementSpeedModifiersEvent.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(LimbDetachmentEffectsSystem))]
public sealed partial class MissingLimbMovementModifierComponent : Component
{
    [DataField]
    public float WalkSpeedModifier = 0.6f;

    [DataField]
    public float SprintSpeedModifier = 0.6f;
}
