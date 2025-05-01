using Content.Server._Harmony.Roles;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Server.Roles;
using Content.Shared._Harmony.BloodBrothers.Components;
using Content.Shared._Harmony.BloodBrothers.EntitySystems;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Zombies;
using Robust.Shared.Utility;

namespace Content.Server._Harmony.BloodBrothers.EntitySystems;

public sealed class BloodBrotherSystem : SharedBloodBrotherSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly IServerPreferencesManager _preferencesManager = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSelectionSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFactionSystem = default!;
    [Dependency] private readonly ObjectivesSystem _objectivesSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly TargetObjectiveSystem _targetObjectiveSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InitialBloodBrotherComponent, BloodBrotherConvertActionEvent>(OnBloodBrotherConvert);
        SubscribeLocalEvent<BloodBrotherComponent, MapInitEvent>(OnBloodBrotherMapInit);
    }

    private void OnBloodBrotherConvert(
        Entity<InitialBloodBrotherComponent> entity,
        ref BloodBrotherConvertActionEvent args)
    {
        if (!TryComp<BloodBrotherComponent>(entity, out var originalComp))
            return;

        var (canConvert, failureMessage) = CanConvert(entity, args.Target);

        if (!canConvert)
        {
            _popupSystem.PopupEntity(Loc.GetString(failureMessage), args.Target, entity, PopupType.SmallCaution);
            return;
        }

        var convertedComp = CopyComp(entity, args.Target, originalComp);

        if (!_mindSystem.TryGetMind(args.Target, out var targetMindId, out var targetMind))
            return;

        // Objective setup
        if (!_objectivesSystem.TryCreateObjective((targetMindId, targetMind),
                entity.Comp.ConvertedBrotherObjective,
                out var newObjective))
            return;

        var targetObjective = EnsureComp<TargetObjectiveComponent>(newObjective.Value);

        _targetObjectiveSystem.SetTarget(newObjective.Value, entity, targetObjective);

        _mindSystem.AddObjective(targetMindId, targetMind, newObjective.Value);

        _adminLogManager.Add(
            LogType.Mind,
            LogImpact.Medium,
            $"{ToPrettyString(entity)} converted {args.Target} into a Blood Brother");

        RemCompDeferred<InitialBloodBrotherComponent>(entity);

        Dirty(entity.Owner, originalComp);
        Dirty(args.Target, convertedComp);
    }

    private (bool canConvert, LocId failureMessage) CanConvert(
        Entity<InitialBloodBrotherComponent> entity,
        EntityUid target)
    {
        if (!_mindSystem.TryGetMind(entity, out _, out var converterMind))
        {
            DebugTools.Assert("Blood brother tried to convert but had no mind.");
            Log.Error("Blood brother tried to convert but had no mind.");
            return (false, default); // How would this even happen
        }

        if (!_mindSystem.TryGetMind(target, out var targetMindId, out var targetMind))
            return (false, entity.Comp.MessageConvertFailedNoMind);

        // Stop the blood brother from converting a target.
        foreach (var objective in converterMind.Objectives)
        {
            if (!TryComp<TargetObjectiveComponent>(objective, out var targetObjective))
                continue;

            if (targetObjective.Target == targetMindId)
                return (false, entity.Comp.MessageConvertFailedTarget);
        }

        if (!HasComp<HumanoidAppearanceComponent>(target))
            return (false, entity.Comp.MessageConvertFailedNotHumanoid);

        if (HasComp<ZombieComponent>(target))
            return (false, entity.Comp.MessageConvertFailedZombie);

        if (HasComp<MindShieldComponent>(target))
            return (false, entity.Comp.MessageConvertFailedMindShielded);

        if (!_mobStateSystem.IsAlive(target))
            return (false, entity.Comp.MessageConvertFailedDead);

        if (targetMind.UserId == null)
            return (false, entity.Comp.MessageConvertFailedNoMind);

        if (entity.Comp.IgnorePreference ||
            !_preferencesManager.TryGetCachedPreferences(targetMind.UserId.Value, out var preferences))
            return (true, default);

        var profile = (HumanoidCharacterProfile)preferences.SelectedCharacter;

        if (profile.AntagPreferences.Contains(entity.Comp.RequiredAntagPreference) != true)
            return (false, entity.Comp.MessageConvertFailedPreference);

        return (true, default);
    }

    private void OnBloodBrotherMapInit(Entity<BloodBrotherComponent> entity, ref MapInitEvent args)
    {
        _npcFactionSystem.AddFaction(entity.Owner, entity.Comp.BloodBrotherFaction);

        if (!_mindSystem.TryGetMind(entity, out var mindId, out var mind))
            return;

        if (mindId == default || !_roleSystem.MindHasRole<BloodBrotherRoleComponent>(mindId))
            _roleSystem.MindAddRole(mindId, entity.Comp.BloodBrotherMindRole);

        if (mind.Session != null)
        {
            _antagSelectionSystem.SendBriefing(
                mind.Session,
                Loc.GetString(entity.Comp.BriefingText),
                entity.Comp.BriefingColor,
                null);
        }
    }
}
