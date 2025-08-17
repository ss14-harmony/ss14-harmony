using Content.Server._Harmony.Objectives.Systems;

namespace Content.Server._Harmony.Objectives.Components;

/// <summary>
/// Requires that the Malf AI set off a doomsday device.
/// </summary>
[RegisterComponent, Access(typeof(DoomsdayConditionSystem))]
public sealed partial class DoomsdayConditionComponent : Component
{
}
