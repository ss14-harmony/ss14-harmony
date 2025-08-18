using Robust.Shared.Audio;

namespace Content.Shared._Harmony.Malfunction.Components;

/// <summary>
/// Marks that an entity has been targeted with Overload Machine and will soon explode.
/// </summary>
[RegisterComponent]
public sealed partial class PendingOverloadComponent : Component
{
    [DataField] public string ExplosionType = "Minibomb"; // these values are equivalent to a minibomb; no idea what it was like in SS13 or if this is comparable but it should work
    [DataField] public float TotalIntensity = 200;
    [DataField] public float Slope = 30;
    [DataField] public float MaxTileIntensity = 60;
    [DataField] public float DetonationDuration = 3;
    public float TimeUntilDetonation;

    public SoundSpecifier OverloadSound = new SoundPathSpecifier("/Audio/Machines/alarm.ogg");
}
