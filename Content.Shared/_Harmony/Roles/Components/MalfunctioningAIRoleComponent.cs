using Content.Shared.Roles.Components;
using Robust.Shared.Audio;

namespace Content.Shared._Harmony.Roles.Components;

/// <summary>
///     Added to mind role entities to tag that they are a malfunctioning AI.
/// </summary>
[RegisterComponent]
public sealed partial class MalfunctioningAIRoleComponent : BaseMindRoleComponent
{
    [DataField] public EntityUid? Action;
    [DataField] public SoundSpecifier HackSound = new SoundCollectionSpecifier("sparks");
    [DataField] public int HackApcTime = 15; // time taken to fully hack an APC

    public float CurrentHackCooldown = 0;
}
