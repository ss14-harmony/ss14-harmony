using Content.Shared._Harmony.BloodBrothers.Components;
using Content.Shared.Actions;
using Content.Shared.Antag;
using Content.Shared.IdentityManagement;
using Content.Shared.Mindshield.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Shared._Harmony.BloodBrothers.EntitySystems;

public abstract partial class SharedBloodBrotherSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedStunSystem _stunSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InitialBloodBrotherComponent, MapInitEvent>(OnInitialBloodBrotherMapInit);
        SubscribeLocalEvent<InitialBloodBrotherComponent, ComponentShutdown>(OnInitialBloodBrotherShutdown);
        SubscribeLocalEvent<BloodBrotherComponent, ComponentGetStateAttemptEvent>(OnBloodBrotherAttemptGetState);
    }

    private void OnInitialBloodBrotherMapInit(Entity<InitialBloodBrotherComponent> entity, ref MapInitEvent args)
    {
        _actionsSystem.AddAction(entity, ref entity.Comp.ConvertActionEntity, entity.Comp.ConvertAction);
        _actionsSystem.AddAction(entity, ref entity.Comp.CheckConvertActionEntity, entity.Comp.CheckConvertAction);
        Dirty(entity);
    }

    private void OnInitialBloodBrotherShutdown(Entity<InitialBloodBrotherComponent> entity, ref ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(entity.Comp.ConvertActionEntity);
        _actionsSystem.RemoveAction(entity.Comp.CheckConvertActionEntity);
    }

    private void OnBloodBrotherAttemptGetState(
        Entity<BloodBrotherComponent> entity,
        ref ComponentGetStateAttemptEvent args)
    {
        args.Cancelled = !CanGetState(args.Player);
    }

    public void OnBloodBrotherMindshielded(EntityUid implanted)
    {
        if (HasComp<InitialBloodBrotherComponent>(implanted))
            return;

        if (!TryComp<BloodBrotherComponent>(implanted, out var bloodBrother))
            return;

        var name = Identity.Entity(implanted, EntityManager);
        RemCompDeferred<BloodBrotherComponent>(implanted);
        if (bloodBrother.DeconversionStunTime != null)
            _stunSystem.TryUpdateParalyzeDuration(implanted, bloodBrother.DeconversionStunTime);
        _popupSystem.PopupEntity(
            Loc.GetString("blood-brother-break-control", ("name", name)),
            implanted,
            PopupType.MediumCaution);
    }

    private bool CanGetState(ICommonSession? player)
    {
        //Apparently this can be null in replays so I am just returning true.
        if (player?.AttachedEntity is not {} uid)
            return true;

        return HasComp<BloodBrotherComponent>(uid) || HasComp<ShowAntagIconsComponent>(uid);
    }
}
