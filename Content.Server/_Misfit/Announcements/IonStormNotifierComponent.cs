namespace Content.Server._Misfit.Announcements;

/// <summary>
/// Used to notify players of an incoming ion storm.
/// </summary>
[RegisterComponent]
public sealed partial class IonStormNotifierComponent : Component
{
    /// <summary>
    /// The chance that the synth is alerted of an ion storm
    /// </summary>
    [DataField]
    public float AlertChance = 0.3f;

    /// <summary>
    /// The text shown to the creature in the notification
    /// </summary>
    [DataField]
    public string Loc = "station-event-ion-storm-synth";
}
