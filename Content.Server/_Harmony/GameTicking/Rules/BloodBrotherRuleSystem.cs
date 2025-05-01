using System.Diagnostics.CodeAnalysis;
using Content.Server._Harmony.GameTicking.Rules.Components;
using Content.Server._Harmony.Roles;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Server.Roles;
using Content.Shared._Harmony.BloodBrothers.Components;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Zombies;
using Robust.Server.Player;
using Robust.Shared.Utility;

namespace Content.Server._Harmony.GameTicking.Rules;

public sealed class BloodBrotherRuleSystem : GameRuleSystem<BloodBrotherRuleComponent>
{
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerPreferencesManager _preferencesManager = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSystem = default!;
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
    }

    private void OnBloodBrotherConvert(Entity<InitialBloodBrotherComponent> entity,
        ref BloodBrotherConvertActionEvent args)
    {
        if (!TryComp<BloodBrotherComponent>(entity, out var originalComponent))
            return;

        var (canConvert, failureMessage) = CanConvert(entity, args.Target);

        if (!canConvert)
        {
            _popupSystem.PopupEntity(Loc.GetString(failureMessage), args.Target, entity, PopupType.SmallCaution);
            return;
        }

        if (!_mindSystem.TryGetMind(entity, out var mindId, out var mind))
            return;

        if (!_mindSystem.TryGetMind(args.Target, out var targetMindId, out var targetMind))
            return;

        var convertedComp = CopyComp(entity, args.Target, originalComponent);

        _npcFactionSystem.AddFaction(args.Target, convertedComp.BloodBrotherFaction);

        _adminLogManager.Add(LogType.Mind,
            LogImpact.Medium,
            $"{ToPrettyString(entity)} converted {ToPrettyString(args.Target)} into their Blood Brother");

        if (_roleSystem.MindHasRole<BloodBrotherRoleComponent>(mindId, out var role))
            role.Value.Comp2.Brother = targetMindId;

        Entity<MindRoleComponent, BloodBrotherRoleComponent>? targetRole = null;
        if (!_roleSystem.MindHasRole(targetMindId, out targetRole))
        {
            _roleSystem.MindAddRole(targetMindId, convertedComp.BloodBrotherMindRole, targetMind);
            _roleSystem.MindHasRole(targetMindId, out targetRole);
        }

        DebugTools.AssertNotNull(targetRole, "Blood brother role was null after assigning it.");

        targetRole!.Value.Comp2.Brother = entity;

        if (!_objectivesSystem.TryCreateObjective((targetMindId, targetMind),
                entity.Comp.ConvertedBrotherObjective,
                out var newObjective))
            return;

        var targetObjective = EnsureComp<TargetObjectiveComponent>(newObjective.Value);

        _targetObjectiveSystem.SetTarget(newObjective.Value, mindId, targetObjective);

        _mindSystem.AddObjective(targetMindId, targetMind, newObjective.Value);

        RemCompDeferred<InitialBloodBrotherComponent>(entity);

        Dirty(entity, originalComponent);
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

        // Target is already a blood brother
        if (HasComp<BloodBrotherRoleComponent>(target))
            return (false, entity.Comp.MessageConvertFailedAlreadyBrother);

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

    /// <summary>
    /// Tries to get the blood brother rule from a blood brother
    /// </summary>
    private bool TryGetBloodBrotherRule(Entity<BloodBrotherComponent> entity,
        [NotNullWhen(true)] out Entity<BloodBrotherRuleComponent>? bloodBrotherRule)
    {
        var allRules = QueryAllRules();
        while (allRules.MoveNext(out var uid, out var bloodBrother, out _))
        {
            if (!TryComp<AntagSelectionComponent>(uid, out var antagSelection))
                continue;

            foreach (var session in antagSelection.AssignedSessions)
            {
                if (session.AttachedEntity != entity.Owner)
                    continue;

                bloodBrotherRule = (uid, bloodBrother);
                return true;
            }
        }

        bloodBrotherRule = null;
        return false;
    }
}
