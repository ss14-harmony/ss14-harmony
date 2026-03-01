using Content.Server.Shuttles.Systems;
using Content.Server._Harmony.GameTicking.Rules.Components;
using Content.Server._Harmony.Objectives.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Shared.Player;

namespace Content.Server._Harmony.Objectives.Systems;

/// <summary>
/// Handles crew containment logic.
/// </summary>
public sealed class ExterminateCrewConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExterminateCrewConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, ExterminateCrewConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress();
    }

    private List<EntityUid> GetTargets()
    {
        var targets = new List<EntityUid>();
        var players = AllEntityQuery<HumanoidProfileComponent, ActorComponent>();
        while (players.MoveNext(out var uid, out _, out _))
        {
            targets.Add(uid);
        }
        return targets;
    }

    private float GetProgress()
    {
        var targets = GetTargets();
        var contained = 0;

        foreach (var target in targets)
        {
            if (!_emergencyShuttle.IsTargetEscaping(target))
                contained++;
        }

        var query = EntityQueryEnumerator<MalfunctioningAIRuleComponent>();
        while (query.MoveNext(out var rule, out var component))
            if (component.DoomsdayActivated)
                return 1f; // doomsday device equals free greentext

        // Failed if evac isn't here yet
        if (!_emergencyShuttle.EmergencyShuttleArrived)
            return 0f;

        if (targets.Count == 0) // if somehow there's no entities left I'd call the crew exterminated
            return 1f;

        return contained / targets.Count;
    }
}
