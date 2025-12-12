using Content.Server._Harmony.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server._Harmony.Objectives.Systems;

public sealed class SelfAndTargetSurviveConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly TargetObjectiveSystem _targetObjectiveSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SelfAndTargetSurviveConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<SelfAndTargetSurviveConditionComponent> entity, ref ObjectiveGetProgressEvent args)
    {
        var progress = _mindSystem.IsCharacterDeadIc(args.Mind) ? 0f : 1f;

        if (_targetObjectiveSystem.GetTarget(entity, out var target) &&
            target != EntityUid.Invalid &&
            _mindSystem.TryGetMind(target.Value, out _, out var mind))
        {
            progress *= 0.5f;
            progress += _mindSystem.IsCharacterDeadIc(mind) ? 0f : 0.5f;
        }

        args.Progress = progress;
    }
}
