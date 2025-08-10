using Content.Server._Harmony.Objectives.Systems;

namespace Content.Server._Harmony.Objectives.Components;

[RegisterComponent, Access(typeof(SelfAndTargetSurviveConditionSystem))]
public sealed partial class SelfAndTargetSurviveConditionComponent : Component;
