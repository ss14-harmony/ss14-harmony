using Content.Server._Harmony.Objectives.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Player;


namespace Content.Server._Harmony.Objectives.Systems;

/// <summary>
/// Handles department extermination logic.
/// </summary>
public sealed class ExterminateDepartmentConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExterminateDepartmentConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, ExterminateDepartmentConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress(comp.Department);
    }

    private List<EntityUid> GetTargets(string department)
    {
        var targets = new List<EntityUid>();
        var players = AllEntityQuery<HumanoidAppearanceComponent, ActorComponent>();
        while (players.MoveNext(out var uid, out _, out _))
        {
            if (!_job.MindTryGetJobId(uid, out var jobId) || jobId is null) continue;
            if (!_job.TryGetAllDepartments(jobId, out var departmentProtos)) continue;
            foreach (DepartmentPrototype departmentProto in departmentProtos)
            {
                if (departmentProto.ID == department)
                {
                    targets.Add(uid);
                }
            }
        }
        return targets;
    }

    private float GetProgress(string department)
    {

        var targets = GetTargets(department);
        var exterminated = 0;

        foreach (EntityUid target in targets)
        {
            // deleted or gibbed or something, counts as dead
            if (!TryComp<MindComponent>(target, out var mind) || mind.OwnedEntity == null)
            {
                exterminated++;
                continue;
            }
            var targetDead = _mind.IsCharacterDeadIc(mind);

            if (targetDead)
                exterminated++;

        }

        if (targets.Count == 0) // if somehow there's no entities left in that department I'd call it exterminated
            return 1f;

        return exterminated / targets.Count;
    }
}
