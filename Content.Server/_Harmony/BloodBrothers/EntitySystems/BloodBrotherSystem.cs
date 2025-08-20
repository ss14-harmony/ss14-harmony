using Content.Server._Harmony.Objectives.Components;
using Content.Server.Mind;
using Content.Server.Objectives.Systems;
using Content.Server.Roles;
using Content.Shared._Harmony.BloodBrothers.Components;
using Content.Shared._Harmony.BloodBrothers.EntitySystems;
using Content.Shared._Harmony.Roles.Components;

namespace Content.Server._Harmony.BloodBrothers.EntitySystems;

public sealed class BloodBrotherSystem : SharedBloodBrotherSystem
{
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly TargetObjectiveSystem _targetObjectiveSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBrotherComponent, ComponentShutdown>(OnBloodBrotherShutdown);
    }

    private void OnBloodBrotherShutdown(Entity<BloodBrotherComponent> entity, ref ComponentShutdown args)
    {
        if (!_mindSystem.TryGetMind(entity, out var mindId, out var mind))
            return;

        if (_roleSystem.MindHasRole<BloodBrotherRoleComponent>(mindId, out var role))
        {
            // Initial no longer has to worry about keeping the converted alive or on the shuttle
            if (role.Value.Comp2.Brother != null &&
                _mindSystem.TryGetMind(role.Value.Comp2.Brother.Value, out _, out var brotherMind))
            {
                foreach (var objective in brotherMind.Objectives)
                {
                    if (!HasComp<BloodBrotherTargetComponent>(objective))
                        continue;

                    _targetObjectiveSystem.SetTarget(objective, EntityUid.Invalid);
                }
            }

            _roleSystem.MindRemoveRole<BloodBrotherRoleComponent>(mindId);
        }

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
