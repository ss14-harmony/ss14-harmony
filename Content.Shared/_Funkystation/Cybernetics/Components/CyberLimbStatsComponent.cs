using Robust.Shared.GameStates;

namespace Content.Shared.Cybernetics.Components;

/// <summary>
/// Caches cyber limb stats for a body. Service time is a shared pool (base per limb + matter bins) that drains when limbs are installed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberLimbStatsComponent : Component
{
    /// <summary>
    /// Remaining service time in the shared pool. Computed as BaseServiceRemaining + sum(matter bin ServiceRemaining).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ServiceTimeRemaining { get; set; }

    /// <summary>
    /// Maximum service time when all limbs are freshly repaired. Computed as BaseServiceTimePerLimb * limbCount + sum(matter bin ServiceTime).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ServiceTimeMax { get; set; }

    /// <summary>
    /// Efficiency multiplier. Limb efficiency from CPUs, multiplied by external modifiers (e.g. 0.5 when depleted).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Efficiency { get; set; } = 1f;

    /// <summary>
    /// Minimum service time per cyber limb. Limbs function without modules (poorly) with this base.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan BaseServiceTimePerLimb { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Remaining base service time. 5 min per limb when limb installed; drains at 1 sec/sec.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan BaseServiceRemaining { get; set; }

    /// <summary>
    /// Sum of charge across all batteries in cyber limb storage. Computed by CyberLimbStatsSystem.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BatteryRemaining { get; set; }

    /// <summary>
    /// Sum of MaxCharge across all batteries. 0 when no batteries.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BatteryMax { get; set; }

    /// <summary>
    /// Base battery drain rate in joules per second (watts). PowerCellMedium 720 J / 20 min = 0.6 J/s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BaseBatteryDrainPerSecond { get; set; } = 0.6f;
}
