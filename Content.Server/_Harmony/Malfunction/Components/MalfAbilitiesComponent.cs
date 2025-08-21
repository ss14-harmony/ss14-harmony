using Content.Server._Harmony.Malfunction.Systems;

namespace Content.Server._Harmony.Malfunction.Components;

/// <summary>
/// Keeps track of a Malfunctioning AI's unlocked abilities and how many uses each has.
/// </summary>
[RegisterComponent, Access(typeof(MalfAbilitiesSystem))]
public sealed partial class MalfAbilitiesComponent : Component
{
    [DataField] public int MachineOverloadUses = 0;
    [DataField] public int OverrideAiaUses = 0;
    [DataField] public int DisableControlPanelUses = 0;
    [DataField] public int OverrideSafetyUses = 0;
    [DataField] public int OverloadLightUses = 0;
    [DataField] public int JamFirelockUses = 0;

    [DataField] public bool VoiceModulation = false;

}