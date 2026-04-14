using Robust.Shared.GameStates;

namespace Content.Shared.Cybernetics.Components;

/// <summary>
/// Tracks maintenance state for a body with cyber limbs. One component per body.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticsMaintenanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool PanelOpen { get; set; }

    [DataField, AutoNetworkedField]
    public bool PanelSecured { get; set; } = true;

    /// <summary>
    /// When true, storage is accessible. When false, bolts are loose and wire repair is required.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BoltsTight { get; set; } = true;

    /// <summary>
    /// Number of wires inserted in the current repair session. Persists when panel is closed early;
    /// only resets when wrench tightens after all wires inserted (repair complete).
    /// </summary>
    [DataField, AutoNetworkedField]
    public int WiresInsertedCount { get; set; }

    /// <summary>
    /// Set when panel is opened with a non-precision tool. Adds flat +5 integrity penalty (binary, once per repair).
    /// Cleared when repair completes (bolts tightened).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool UnskilledRepairThisSession { get; set; }
}
