using Content.Server._Harmony.Roles;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared._Harmony.BloodBrothers.Components;
using Content.Shared._Harmony.BloodBrothers.EntitySystems;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Zombies;

namespace Content.Server._Harmony.BloodBrothers.EntitySystems;

public sealed class BloodBrotherSystem : SharedBloodBrotherSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSelectionSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFactionSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;

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
        if (!_mindSystem.TryGetMind(target, out _, out _))
            return (false, entity.Comp.MessageConvertFailedNoMind);

        if (!HasComp<HumanoidAppearanceComponent>(target))
            return (false, entity.Comp.MessageConvertFailedNotHumanoid);

        if (HasComp<ZombieComponent>(target))
            return (false, entity.Comp.MessageConvertFailedZombie);

        if (HasComp<MindShieldComponent>(target))
            return (false, entity.Comp.MessageConvertFailedMindShielded);

        if (!_mobStateSystem.IsAlive(target))
            return (false, entity.Comp.MessageConvertFailedDead);

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
