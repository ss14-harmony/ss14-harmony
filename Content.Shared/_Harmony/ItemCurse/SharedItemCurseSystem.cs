using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Shared._Harmony.ItemCurse;

/// <summary>
/// System for handling the ItemCurse ability for wizards.
/// This is pretty much a copy of SharedItemRecallSystem with the resulting effect on the marked item changed.
/// </summary>
public abstract class SharedItemCurseSystem : EntitySystem
{
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
        Dirty(ent);
    }

    private void OnItemCurseActionUse(Entity<ItemCurseComponent> ent, ref OnItemCurseActionEvent args)
    {
        if (ent.Comp.MarkedEntity == null)
        {
            if (!TryComp<HandsComponent>(args.Performer, out var hands))
                return;

            if (!_hands.TryGetActiveItem((args.Performer, hands), out var markItem))
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

        // Finger snap emote because it's Cool.
        Snap(args.Performer, ent.Comp);
        CurseItem(ent.Comp.MarkedEntity.Value, ent.Comp);
        args.Handled = true;
    }

    private void CurseItem(Entity<CurseMarkerComponent?> ent, ItemCurseComponent comp)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        if (_actions.GetAction(ent.Comp.MarkedByAction) is not { } action)
            return;

        if (action.Comp.AttachedEntity is not { } user)
            return;

        // Un-embed embeddable projectiles
        if (TryComp<EmbeddableProjectileComponent>(ent, out var projectile))
            _proj.EmbedDetach(ent, projectile, user);

        // Check if the cursed item is in any container.
        // If it is, check each outer container until either finding a body to target or running out of containers.
        // The first body found will be targeted by the direct shocking effect.
        var containerEnt = ent;
        while (_containerSystem.TryGetContainingContainer((containerEnt, null, null), out var nextContainer))
        {
            if (HasComp<BodyComponent>(nextContainer.Owner))    // Is checking for the body comp the right way to do this? I hope so.
            {
                ShockHolder(nextContainer.Owner, ent, comp);
                break;
            }
            containerEnt = nextContainer.Owner;
        }

        // If the entity is in a container, place it into the world instead
        if (_containerSystem.IsEntityInContainer(ent))
        {
            _transform.SetCoordinates(ent, Transform(ent.Owner).Coordinates);
            _transform.AttachToGridOrMap(ent);
        }

        // Give it a good chuck
        _throwing.TryThrow(ent, _random.NextVector2(), baseThrowSpeed: comp.FlingStrength);
        CreateLightning(ent, comp);
    }

    private void OnCurseMarkerShutdown(Entity<CurseMarkerComponent> ent, ref ComponentShutdown args)
    {
        TryUnmarkItem(ent);
    }

    private void TryMarkItem(Entity<ItemCurseComponent> ent, EntityUid item)
    {
        if (_actions.GetAction(ent.Owner) is not { } action)
            return;

        if (action.Comp.AttachedEntity is null)
            return;

        ent.Comp.MarkedEntity = item;
        Dirty(ent);

        var marker = AddComp<CurseMarkerComponent>(item);
        marker.MarkedByAction = ent;
        Dirty(item, marker);

        UpdateActionAppearance((action, action, ent));
    }

    private void TryUnmarkItem(Entity<CurseMarkerComponent> item)
    {
        if (!TryComp<CurseMarkerComponent>(item, out var marker))
            return;

        if (_actions.GetAction(marker.MarkedByAction) is not { } action)
            return;

        if (TryComp<ItemCurseComponent>(action, out var itemCurse))
        {
            // The following comment was from the recall code I copied. I'll just leave it here because it's probably important. TL;DR this code doesn't work yet.
            //
            // For some reason client thinks the station grid owns the action on client and this doesn't work. It doesn't work in PopupEntity(mispredicts) and PopupPredicted either(doesnt show).
            // I don't have the heart to move this code to server because of this small thing.
            // This line will only do something once that is fixed.
            if (action.Comp.AttachedEntity is { } user)
            {
                _popups.PopupClient(Loc.GetString("item-recall-item-unmark", ("item", item)), user, user, PopupType.MediumCaution);
            }

            itemCurse.MarkedEntity = null;
            UpdateActionAppearance((action, action, itemCurse));
            Dirty(action, itemCurse);
        }

        RemCompDeferred<CurseMarkerComponent>(item);
    }

    private void UpdateActionAppearance(Entity<ActionComponent, ItemCurseComponent> action)
    {
        if (action.Comp2.MarkedEntity is { } marked)
        {
            if (action.Comp2.WhileMarkedName is { } name)
                _metaData.SetEntityName(action, Loc.GetString(name, ("item", marked)));

            if (action.Comp2.WhileMarkedDescription is { } desc)
                _metaData.SetEntityDescription(action, Loc.GetString(desc, ("item", marked)));

            _actions.SetEntityIcon((action, action), marked);
        }
        else
        {
            if (action.Comp2.InitialName is { } name)
                _metaData.SetEntityName(action, name);

            if (action.Comp2.InitialDescription is { } desc)
                _metaData.SetEntityDescription(action, desc);

            _actions.SetEntityIcon((action, action), null);
        }
    }

    // Methods to be overridden in server ItemCurseSystem
    protected virtual void CreateLightning(EntityUid sourceUid, ItemCurseComponent spellComp)
    {

    }

    protected virtual void ShockHolder(EntityUid targetUid, EntityUid sourceUid, ItemCurseComponent spellComp)
    {

    }

    protected virtual void Snap(EntityUid targetUid, ItemCurseComponent spellComp)
    {

    }
}
