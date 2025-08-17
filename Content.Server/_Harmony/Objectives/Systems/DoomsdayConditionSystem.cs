using Content.Server._Harmony.Objectives.Components;
using Content.Server._Harmony.GameTicking.Rules.Components;
using Content.Shared.Objectives.Components;

namespace Content.Server._Harmony.Objectives.Systems;

/// <summary>
/// Handles progress for the "activate a doomsday device" condition.
/// </summary>
public sealed class DoomsdayConditionSystem : EntitySystem
{

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DoomsdayConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, DoomsdayConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress();
    }

    private float GetProgress()
    {
        var query = EntityQueryEnumerator<MalfunctioningAIRuleComponent>();
        while (query.MoveNext(out var rule, out var component))
            if (component.DoomsdayActivated)
                return 1f; // doomsday device equals free greentext

        return 0f;
    }
}
