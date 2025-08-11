using Content.Server._Harmony.Objectives.Components;
using Content.Server._Harmony.Roles;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Harmony.BloodBrothers.Components;
using Content.Shared._Harmony.BloodBrothers.EntitySystems;

namespace Content.Server._Harmony.BloodBrothers.EntitySystems;

public sealed class BloodBrotherSystem : SharedBloodBrotherSystem
{
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBrotherComponent, ComponentShutdown>(OnBloodBrotherShutdown);
    }

    private void OnBloodBrotherShutdown(Entity<BloodBrotherComponent> entity, ref ComponentShutdown args)
    {
        if (!_mindSystem.TryGetMind(entity, out var mindId, out var mind))
            return;

        if (_roleSystem.MindHasRole<BloodBrotherRoleComponent>(mindId))
            _roleSystem.MindRemoveRole<BloodBrotherRoleComponent>(mindId);

        int? objectiveToRemove = null;

        var i = 0;
        foreach (var objective in mind.Objectives)
        {
            if (HasComp<ConvertedBloodBrotherObjectiveComponent>(objective))
            {
                objectiveToRemove = i;
                break;
            }

            i++;
        }

        if (objectiveToRemove != null)
            _mindSystem.TryRemoveObjective(mindId, mind, objectiveToRemove.Value);
    }
}
