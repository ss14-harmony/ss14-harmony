namespace Content.Server._Harmony.Anomaly.Components;

/// <summary>
/// Marks an anomaly so that its severity is not increased by an anomaly generator hack.
/// </summary>
[RegisterComponent]
public sealed partial class RegularAnomalyComponent : Component;
