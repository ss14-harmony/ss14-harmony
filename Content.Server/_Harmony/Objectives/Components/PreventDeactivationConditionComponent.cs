using Content.Server._Harmony.Objectives.Systems;

namespace Content.Server._Harmony.Objectives.Components;

/// <summary>
/// Requires that the AI is not detached, in an intellicard or dead.
/// </summary>
[RegisterComponent, Access(typeof(PreventDeactivationConditionSystem))]
public sealed partial class PreventDeactivationConditionComponent : Component
{
}
