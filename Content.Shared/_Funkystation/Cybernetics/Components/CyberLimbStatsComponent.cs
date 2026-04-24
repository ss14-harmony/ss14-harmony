using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

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
    /// Arm efficiency multiplier. Driven by CPUs installed in cyber arms (+10% per CPU), multiplied by external modifiers (e.g. 0.5 when depleted).
    /// Controls interaction / do-after speed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ArmEfficiency { get; set; } = 1f;

    /// <summary>
    /// Leg efficiency multiplier. Driven by CPUs installed in cyber legs (+5% per CPU), multiplied by external modifiers (e.g. 0.5 when depleted).
    /// Controls movement speed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LegEfficiency { get; set; } = 1f;

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

    /// <summary>
    /// True after a low-service (&lt;25%) warning until service recovers to at least 25%.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool LowMaintenanceWarned { get; set; }

    /// <summary>
    /// Next game time at which another low-service popup may be shown while still below threshold.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextMaintenanceWarning { get; set; }

    /// <summary>
    /// True after a low-battery (&lt;25%) warning until battery recovers to at least 25%.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool LowPowerWarned { get; set; }

    /// <summary>
    /// Next game time at which another low-power popup may be shown while still below threshold.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextPowerWarning { get; set; }
}
