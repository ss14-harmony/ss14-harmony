using Content.Shared.Actions;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Shared.ItemCurse;

/// <summary>
/// System for handling the ItemCurse ability for wizards.
/// This is pretty much an exact copy of SharedItemRecallSystem.
/// </summary>
public abstract partial class SharedItemCurseSystem : EntitySystem
{
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedPvsOverrideSystem _pvs = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly SharedProjectileSystem _proj = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemCurseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ItemCurseComponent, OnItemCurseActionEvent>(OnItemCurseActionUse);

        SubscribeLocalEvent<CurseMarkerComponent, ComponentShutdown>(OnCurseMarkerShutdown);
    }

    private void OnMapInit(Entity<ItemCurseComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.InitialName = Name(ent);
        ent.Comp.InitialDescription = Description(ent);
    }

    private void OnItemCurseActionUse(Entity<ItemCurseComponent> ent, ref OnItemCurseActionEvent args)
    {
        if (ent.Comp.MarkedEntity == null)
        {
            if (!TryComp<HandsComponent>(args.Performer, out var hands))
                return;

            var markItem = _hands.GetActiveItem((args.Performer, hands));

            if (markItem == null)
            {
                _popups.PopupClient(Loc.GetString("item-recall-item-mark-empty"), args.Performer, args.Performer);
                return;
            }

            if (HasComp<CurseMarkerComponent>(markItem))
            {
                _popups.PopupClient(Loc.GetString("item-recall-item-already-marked", ("item", markItem)), args.Performer, args.Performer);
                return;
            }

            _popups.PopupClient(Loc.GetString("item-recall-item-marked", ("item", markItem.Value)), args.Performer, args.Performer);
            TryMarkItem(ent, markItem.Value);
            return;
        }
        Log.Debug("a");
        Snap(args.Performer);
        CurseItem(ent.Comp.MarkedEntity.Value, ent.Comp);
        args.Handled = true;
    }

    private void CurseItem(Entity<CurseMarkerComponent?> ent, ItemCurseComponent comp)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        if (!TryComp<InstantActionComponent>(ent.Comp.MarkedByAction, out var instantAction))
            return;

        var actionOwner = instantAction.AttachedEntity;

        if (actionOwner == null)
            return;

        if (TryComp<EmbeddableProjectileComponent>(ent, out var projectile))
            _proj.EmbedDetach(ent, projectile, actionOwner.Value);

        if (_containerSystem.TryGetOuterContainer(ent, Transform(ent), out var holder))
        {
            _transform.SetCoordinates(ent, Transform(holder.Owner).Coordinates);
            _transform.AttachToGridOrMap(ent);
            ShockHolder(holder.Owner, ent, comp);
        }
        _throwing.TryThrow(ent, _random.NextVector2(), baseThrowSpeed: comp.FlingStrength);

        CreateLightning(ent, comp);
    }

    private void OnCurseMarkerShutdown(Entity<CurseMarkerComponent> ent, ref ComponentShutdown args)
    {
        TryUnmarkItem(ent);
    }

    private void TryMarkItem(Entity<ItemCurseComponent> ent, EntityUid item)
    {
        if (!TryComp<InstantActionComponent>(ent, out var instantAction))
            return;

        var actionOwner = instantAction.AttachedEntity;

        if (actionOwner == null)
            return;

        AddToPvsOverride(item, actionOwner.Value);

        var marker = AddComp<CurseMarkerComponent>(item);
        ent.Comp.MarkedEntity = item;
        Dirty(ent);

        marker.MarkedByAction = ent.Owner;

        UpdateActionAppearance(ent);
        Dirty(item, marker);
    }

    private void TryUnmarkItem(EntityUid item)
    {
        if (!TryComp<CurseMarkerComponent>(item, out var marker))
            return;

        if (!TryComp<InstantActionComponent>(marker.MarkedByAction, out var instantAction))
            return;

        if (TryComp<ItemCurseComponent>(marker.MarkedByAction, out var action))
        {
            // The following comment was from the recall code I copied. I'll just leave it here because it's probably important. TL;DR this code doesn't work yet.
            //
            // For some reason client thinks the station grid owns the action on client and this doesn't work. It doesn't work in PopupEntity(mispredicts) and PopupPredicted either(doesnt show).
            // I don't have the heart to move this code to server because of this small thing.
            // This line will only do something once that is fixed.
            if (instantAction.AttachedEntity != null)
            {
                _popups.PopupClient(Loc.GetString("item-recall-item-unmark", ("item", item)), instantAction.AttachedEntity.Value, instantAction.AttachedEntity.Value, PopupType.MediumCaution);
                RemoveFromPvsOverride(item, instantAction.AttachedEntity.Value);
            }

            action.MarkedEntity = null;
            UpdateActionAppearance((marker.MarkedByAction.Value, action));
            Dirty(marker.MarkedByAction.Value, action);
        }

        RemCompDeferred<CurseMarkerComponent>(item);
    }

    private void UpdateActionAppearance(Entity<ItemCurseComponent> action)
    {
        if (!TryComp<InstantActionComponent>(action, out var instantAction))
            return;

        if (action.Comp.MarkedEntity == null)
        {
            if (action.Comp.InitialName != null)
                _metaData.SetEntityName(action, action.Comp.InitialName);
            if (action.Comp.InitialDescription != null)
                _metaData.SetEntityDescription(action, action.Comp.InitialDescription);
            _actions.SetEntityIcon(action, null, instantAction);
        }
        else
        {
            if (action.Comp.WhileMarkedName != null)
                _metaData.SetEntityName(action, Loc.GetString(action.Comp.WhileMarkedName,
                    ("item", action.Comp.MarkedEntity.Value)));

            if (action.Comp.WhileMarkedDescription != null)
                _metaData.SetEntityDescription(action, Loc.GetString(action.Comp.WhileMarkedDescription,
                    ("item", action.Comp.MarkedEntity.Value)));

            _actions.SetEntityIcon(action, action.Comp.MarkedEntity, instantAction);
        }
    }

    private void AddToPvsOverride(EntityUid uid, EntityUid user)
    {
        if (!_player.TryGetSessionByEntity(user, out var mindSession))
            return;

        _pvs.AddSessionOverride(uid, mindSession);
    }

    private void RemoveFromPvsOverride(EntityUid uid, EntityUid user)
    {
        if (!_player.TryGetSessionByEntity(user, out var mindSession))
            return;

        _pvs.RemoveSessionOverride(uid, mindSession);
    }

    public virtual void CreateLightning(EntityUid ent, ItemCurseComponent comp)
    {

    }

    public virtual void ShockHolder(EntityUid ent, EntityUid source, ItemCurseComponent comp)
    {

    }

    public virtual void Snap(EntityUid ent)
    {

    }
}
