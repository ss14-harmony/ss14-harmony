using Content.Server.Shuttles.Systems;
using Content.Server._Harmony.Objectives.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._Harmony.Objectives.Systems;

/// <summary>
/// Handles crew containment logic.
/// </summary>
public sealed class ExterminateCrewConditionSystem : EntitySystem
{
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

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
            targets.Add(uid);
        }
        return targets;
    }

    private float GetProgress(string department)
    {
        var targets = GetTargets(department);
        var contained = 0;

        foreach (EntityUid target in targets)
        {
            // deleted or gibbed or something, counts as marooned
            if (!TryComp<MindComponent>(target, out var mind) || mind.OwnedEntity == null)
            {
                contained++;
                continue;
            }
            var targetDead = _mind.IsCharacterDeadIc(mind);

            if (!_emergencyShuttle.IsTargetEscaping(target))
                contained++;

        }

        // Failed if evac isn't here yet
        if (!_emergencyShuttle.EmergencyShuttleArrived)
            return 0f;

        if (targets.Count == 0) // if somehow there's no entities left in that department I'd call it exterminated
            return 1f;

        return contained / targets.Count;
    }
}
