using Content.Server._Harmony.Objectives.Systems;

namespace Content.Server._Harmony.Objectives.Components;

/// <summary>
/// Requires that no crew escape.
/// </summary>
[RegisterComponent, Access(typeof(ExterminateCrewConditionSystem))]
public sealed partial class ExterminateCrewConditionComponent : Component
{
}
