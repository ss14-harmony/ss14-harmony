using Content.Shared.Cybernetics.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Cybernetics.Components;

/// <summary>
/// Component on matter bin items. Stores service time capacity and remaining runtime.
/// Service resource drains at 1 sec/sec when installed in a cyber limb on a body.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CyberLimbModuleSystem), typeof(CyberLimbStatsSystem))]
public sealed partial class CyberLimbMatterBinComponent : Component
{
    /// <summary>
    /// Service time this matter bin provides when full (e.g. 10 min).
    /// </summary>
    [DataField]
    public TimeSpan ServiceTime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Remaining service time. Drains at 1 sec/sec when body has cyber stats.
    /// Set to zero when inserted into cyber limb storage.
    /// </summary>
    [DataField]
    public TimeSpan ServiceRemaining { get; set; }
}
