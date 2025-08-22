namespace Content.Server._Harmony.Malfunction.Components;

/// <summary>
/// Keeps track of the duration of the Lockdown ability.
/// </summary>
[RegisterComponent]
public sealed partial class LockdownComponent : Component
{
    [DataField] public float Duration = 90;
    [DataField] public float RemainingTime;
}
