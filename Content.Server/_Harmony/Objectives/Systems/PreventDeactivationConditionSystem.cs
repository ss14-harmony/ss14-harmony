using Content.Shared.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared._Harmony.Malfunction.Components;
using Content.Shared.Station.Components;
using Content.Server._Harmony.Objectives.Components;

namespace Content.Server._Harmony.Objectives.Systems;

/// <summary>
/// Handles progress for the "prevent deactivation" objective condition.
/// </summary>
public sealed class PreventDeactivationConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PreventDeactivationConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, PreventDeactivationConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (_mind.IsCharacterDeadIc(args.Mind) || args.Mind.OwnedEntity is null) // destroyed somehow?
            args.Progress = 0f;
        else if (HasComp<IntellicardedComponent>(args.Mind.OwnedEntity)) // carded?
            args.Progress = 0f;
        else if (TryComp<TransformComponent>(args.Mind.OwnedEntity, out var xform) && !HasComp<StationMemberComponent>(xform.GridUid)) // detached?
            args.Progress = 0f;
        else
            args.Progress = 1f;

    }
}
