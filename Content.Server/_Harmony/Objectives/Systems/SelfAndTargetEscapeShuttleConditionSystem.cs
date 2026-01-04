using Content.Server._Harmony.Objectives.Components;
using Content.Server.Mind;
using Content.Server.Objectives.Systems;
using Content.Shared.Objectives.Components;

namespace Content.Server._Harmony.Objectives.Systems;

public sealed class SelfAndTargetEscapeShuttleConditionSystem : EntitySystem
{
    [Dependency] private readonly EscapeShuttleConditionSystem _escapeShuttleConditionSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly TargetObjectiveSystem _targetObjectiveSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SelfAndTargetEscapeShuttleConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<SelfAndTargetEscapeShuttleConditionComponent> entity, ref ObjectiveGetProgressEvent args)
    {
        var progress = _escapeShuttleConditionSystem.GetProgress(args.MindId, args.Mind);

        if (_targetObjectiveSystem.GetTarget(entity, out var target) &&
            target != EntityUid.Invalid &&
            _mindSystem.TryGetMind(target.Value, out var mindId, out var mind))
        {
            progress *= 0.5f;
            progress += _escapeShuttleConditionSystem.GetProgress(mindId, mind) * 0.5f;
        }

        args.Progress = progress;
    }
}
