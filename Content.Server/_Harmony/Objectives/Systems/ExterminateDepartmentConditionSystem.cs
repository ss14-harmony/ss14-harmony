using Content.Server._Harmony.GameTicking.Rules.Components;
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
            if (!_mind.TryGetMind(uid, out var mindId, out _)) continue;
            if (!_job.MindTryGetJobId(mindId, out var jobId) || jobId is null) continue;
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

        foreach (var target in targets)
        {
            // no mind, catatonic or something, counts as dead
            if (!_mind.TryGetMind(target, out var mindId, out var mindComp))
            {
                exterminated++;
                continue;
            }

            if (_mind.IsCharacterDeadIc(mindComp))
                exterminated++;

        }


        if (targets.Count == 0) // if somehow there's no entities left in that department I'd call it exterminated
            return 1f;

        var query = EntityQueryEnumerator<MalfunctioningAIRuleComponent>();
        while (query.MoveNext(out var rule, out var component))
            if (component.DoomsdayActivated)
                return 1f; // doomsday device equals free greentext

        return exterminated / targets.Count;
    }
}
