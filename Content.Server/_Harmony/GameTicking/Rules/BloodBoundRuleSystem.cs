using System.Diagnostics.CodeAnalysis;
using Content.Server._Harmony.GameTicking.Rules.Components;
using Content.Server._Harmony.Roles;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Server.Roles;
using Content.Server.Stunnable;
using Content.Shared._Harmony.BloodOath.Components;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
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

public sealed class BloodBoundRuleSystem : GameRuleSystem<BloodBoundRuleComponent>
{
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerPreferencesManager _preferencesManager = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFactionSystem = default!;
    [Dependency] private readonly ObjectivesSystem _objectivesSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly StunSystem _stunSystem = default!;
    [Dependency] private readonly TargetObjectiveSystem _targetObjectiveSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBoundRuleComponent, ObjectivesTextPrependEvent>(OnObjectivesTextPrepend);
        SubscribeLocalEvent<InitialBloodBoundComponent, BloodBoundConvertActionEvent>(OnBloodBoundConvert);
        SubscribeLocalEvent<InitialBloodBoundComponent, BloodBoundCheckConvertActionEvent>(OnBloodBoundCheckConvert);
    }

    private void OnObjectivesTextPrepend(Entity<BloodBoundRuleComponent> entity, ref ObjectivesTextPrependEvent args)
    {
        var antags = _antagSystem.GetAntagIdentifiers(entity.Owner);

        foreach (var (mind, sessionData, name) in antags)
        {
            if (!_roleSystem.MindHasRole<BloodBoundRoleComponent>(mind, out var role))
                continue;

            var boundRole = role.Value.Comp2;

            if (boundRole.Bound == null)
                continue;

            if (!_mindSystem.TryGetMind(boundRole.Bound.Value, out _, out var boundMind)
                || boundMind.UserId == null)
            {
                args.Text += "\n" + Loc.GetString("blood-bound-round-end-no-mind",
                    ("name", name),
                    ("username", sessionData.UserName),
                    ("boundName", MetaData(role.Value).EntityName));

                continue;
            }

            var boundUsername = _playerManager.GetPlayerData(boundMind.UserId.Value).UserName;

            args.Text += "\n" + Loc.GetString("blood-bound-round-end",
                ("name", name),
                ("username", sessionData.UserName),
                ("boundName", MetaData(boundRole.Bound.Value).EntityName),
                ("boundUsername", (boundUsername)));
        }
    }

    private void OnBloodBoundConvert(Entity<InitialBloodBoundComponent> entity,
        ref BloodBoundConvertActionEvent args)
    {
        // Check if convertible
        if (!TryComp<BloodBoundComponent>(entity, out var originalComponent))
            return;

        if (!CanConvert(entity, args.Target, out var failureMessage))
        {
            _popupSystem.PopupEntity(
                Loc.GetString(failureMessage,
                    ("converter", Identity.Entity(entity, _entityManager)),
                    ("converted", Identity.Entity(args.Target, _entityManager))),
                args.Target,
                entity,
                PopupType.MediumCaution);
            return;
        }

        if (!_mindSystem.TryGetMind(entity, out var mindId, out _))
            return;

        if (!_mindSystem.TryGetMind(args.Target, out var targetMindId, out var targetMind))
            return;

        // Actual conversion logic
        var convertedComp = CopyComp(entity, args.Target, originalComponent);

        _npcFactionSystem.AddFaction(args.Target, entity.Comp.BloodBoundFaction);

        _adminLogManager.Add(LogType.Mind,
            LogImpact.Medium,
            $"{ToPrettyString(entity)} converted {ToPrettyString(args.Target)} into their Blood Bound");

        originalComponent.Bound = args.Target;
        if (_roleSystem.MindHasRole<BloodBoundRoleComponent>(mindId, out var role))
            role.Value.Comp2.Bound = args.Target;

        if (!_roleSystem.MindHasRole(targetMindId, out Entity<MindRoleComponent, BloodBoundRoleComponent>? targetRole))
        {
            _roleSystem.MindAddRole(targetMindId, entity.Comp.BloodBoundMindRole, targetMind);
            _roleSystem.MindHasRole(targetMindId, out targetRole);
        }

        DebugTools.AssertNotNull(targetRole, "Blood bound role was null after assigning it.");

        convertedComp.Bound = entity;
        targetRole!.Value.Comp2.Bound = entity;

        if (!_objectivesSystem.TryCreateObjective((targetMindId, targetMind),
                entity.Comp.ConvertedBoundObjective,
                out var newObjective))
            return;

        var targetObjective = EnsureComp<TargetObjectiveComponent>(newObjective.Value);

        _targetObjectiveSystem.SetTarget(newObjective.Value, mindId, targetObjective);

        _mindSystem.AddObjective(targetMindId, targetMind, newObjective.Value);

        // Visuals
        _antagSystem.SendBriefing(args.Target,
            Loc.GetString(entity.Comp.BriefingText),
            entity.Comp.BriefingColor,
            entity.Comp.BriefingSound);

        _popupSystem.PopupEntity(
            Loc.GetString(
                entity.Comp.ConvertPopupText,
                ("converter", Identity.Entity(entity, _entityManager)),
                ("converted", Identity.Entity(args.Target, _entityManager))),
            args.Target,
            PopupType.LargeCaution);

        if (entity.Comp.ConvertStunTime != null)
            _stunSystem.TryParalyze(args.Target, entity.Comp.ConvertStunTime.Value, true);

        // Cleanup the data
        RemCompDeferred<InitialBloodBoundComponent>(entity);

        Dirty(entity, originalComponent);
        Dirty(args.Target, convertedComp);
    }

    private void OnBloodBoundCheckConvert(Entity<InitialBloodBoundComponent> entity,
        ref BloodBoundCheckConvertActionEvent args)
    {
        if (!CanConvert(entity, args.Target, out var failureMessage))
        {
            _popupSystem.PopupEntity(
                Loc.GetString(failureMessage,
                    ("converter", Identity.Entity(entity, _entityManager)),
                    ("converted", Identity.Entity(args.Target, _entityManager))),
                args.Target,
                entity,
                PopupType.MediumCaution);
            return;
        }

        _popupSystem.PopupEntity(
            Loc.GetString("blood-bound-convert-convertible",
                ("converter", Identity.Entity(entity, _entityManager)),
                ("converted", Identity.Entity(args.Target, _entityManager))),
            args.Target,
            entity,
            PopupType.Medium);
    }

    private bool CanConvert(
        Entity<InitialBloodBoundComponent> entity,
        EntityUid target,
        [NotNullWhen(false)] out string? errorMessage)
    {
        errorMessage = null;

        if (!_mindSystem.TryGetMind(entity, out _, out var converterMind))
        {
            DebugTools.Assert("Blood bound tried to convert but had no mind.");
            Log.Error("Blood bound tried to convert but had no mind.");
            errorMessage = "guh";
            return false; // How would this even happen
        }

        if (!_mindSystem.TryGetMind(target, out var targetMindId, out var targetMind))
        {
            errorMessage = "blood-bound-convert-failed-no-mind";
            return false;
        }

        // Target is already a blood bound
        if (HasComp<BloodBoundComponent>(target))
        {
            errorMessage = "blood-bound-convert-failed-already-bound";
            return false;
        }

        // Stop the blood bound from converting a target.
        foreach (var objective in converterMind.Objectives)
        {
            if (!TryComp<TargetObjectiveComponent>(objective, out var targetObjective))
                continue;

            if (targetObjective.Target != targetMindId)
                continue;

            errorMessage = "blood-bound-convert-failed-target";
            return false;
        }

        if (!HasComp<HumanoidAppearanceComponent>(target))
        {
            errorMessage = "blood-bound-convert-failed-no-mind";
            return false;
        }

        if (HasComp<ZombieComponent>(target))
        {
            errorMessage = "blood-bound-convert-failed-zombie";
            return false;
        }

        if (targetMind.UserId == null)
        {
            errorMessage = "blood-bound-convert-failed-no-mind";
            return false;
        }

        // Check antag preference
        if (entity.Comp.RequiredAntagPreference != null &&
            _preferencesManager.TryGetCachedPreferences(targetMind.UserId.Value, out var preferences))
        {

            var profile = (HumanoidCharacterProfile)preferences.SelectedCharacter;

            if (profile.AntagPreferences.Contains(entity.Comp.RequiredAntagPreference!.Value) != true)
            {
                errorMessage = "blood-bound-convert-failed-preference";
                return false;
            }
        }

        if (!_mobStateSystem.IsAlive(target))
        {
            errorMessage = "blood-bound-convert-failed-dead";
            return false;
        }

        if (HasComp<MindShieldComponent>(target))
        {
            errorMessage = "blood-bound-convert-failed-shielded";
            return false;
        }

        return true;
    }
}
