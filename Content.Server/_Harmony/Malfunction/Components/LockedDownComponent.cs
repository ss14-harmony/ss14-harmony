namespace Content.Server._Harmony.Malfunction.Components;

/// <summary>
/// Keeps track of the original state of a locked down airlock to restore it to that state once the Lockdown ends.
/// </summary>
[RegisterComponent]
public sealed partial class LockedDownComponent : Component
{
    [DataField] public bool Bolted;
    [DataField] public bool? Electrified;
}
