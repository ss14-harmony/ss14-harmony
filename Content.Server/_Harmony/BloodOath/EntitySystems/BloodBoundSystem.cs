using Content.Server._Harmony.Objectives.Components;
using Content.Server._Harmony.Roles;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Harmony.BloodOath.Components;
using Content.Shared._Harmony.BloodOath.EntitySystems;

namespace Content.Server._Harmony.BloodOath.EntitySystems;

public sealed class BloodBoundSystem : SharedBloodBoundSystem
{
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBoundComponent, ComponentShutdown>(OnBloodBoundShutdown);
    }

    private void OnBloodBoundShutdown(Entity<BloodBoundComponent> entity, ref ComponentShutdown args)
    {
        if (!_mindSystem.TryGetMind(entity, out var mindId, out var mind))
            return;

        if (_roleSystem.MindHasRole<BloodBoundRoleComponent>(mindId))
            _roleSystem.MindRemoveRole<BloodBoundRoleComponent>(mindId);

        int? objectiveToRemove = null;

        var i = 0;
        foreach (var objective in mind.Objectives)
        {
            if (HasComp<ConvertedBloodBoundObjectiveComponent>(objective))
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
