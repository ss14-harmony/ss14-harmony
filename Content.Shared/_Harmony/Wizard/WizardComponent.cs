using Robust.Shared.GameStates;

namespace Content.Shared._Harmony.Wizard;

/// <summary>
/// This is used for tagging a mob as a wizard.
/// Component will be transferred on mindswap, so the mob containing the wizard mind will always have the wizard component.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WizardComponent : Component
{

}
