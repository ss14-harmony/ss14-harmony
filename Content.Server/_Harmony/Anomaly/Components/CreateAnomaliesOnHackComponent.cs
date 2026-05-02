namespace Content.Server._Harmony.Anomaly.Components;

[RegisterComponent]
public sealed partial class CreateAnomaliesOnHackComponent : Component
{
    /// <summary>
    /// The number of anomalies to create.
    /// </summary>
    [DataField]
    public int Anomalies = 5;

    /// <summary>
    /// The announcement to make when the beacon is planted.
    /// </summary>
    [DataField]
    public LocId InitialAnnouncement;

    /// <summary>
    /// The announcement to make when the hack takes effect.
    /// </summary>
    [DataField]
    public LocId FinalAnnouncement;

    /// <summary>
    /// The sender of the announcement.
    /// </summary>
    [DataField]
    public LocId AnnouncementSender;

    [DataField]
    public Color AnnouncementColor = Color.Red;

    /// <summary>
    /// The amount of time it takes for the beacon to take effect.
    /// </summary>
    [DataField]
    public TimeSpan HackTime = TimeSpan.FromSeconds(25);

    /// <summary>
    /// The minimum severity of an anomaly created by the hack.
    /// </summary>
    [DataField]
    public float MinSeverity = 0.85f;

    /// <summary>
    /// The maximum severity of an anomaly created by the hack.
    /// </summary>
    [DataField]
    public float MaxSeverity = 0.9f;
}