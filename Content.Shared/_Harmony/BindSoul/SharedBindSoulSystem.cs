using System.Timers;
using Content.Shared._Harmony.ItemCurse;
using Content.Shared.Actions;
using Content.Shared.Beam;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Shared._Harmony.BindSoul;

public abstract class SharedBindSoulSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OnBindSoulActionEvent>(OnSoulBindActionUse);
        SubscribeLocalEvent<SoulBindedComponent, ComponentInit>(OnComponentStartup);
    }

    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly SharedProjectileSystem _proj = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedBeamSystem _beam = default!;

    protected virtual void OnSoulBindActionUse(OnBindSoulActionEvent args)
    {
        if (args.BindedItem != null)
            return;

        if (!TryComp<HandsComponent>(args.Performer, out var hands))
            return;

        if (!_hands.TryGetActiveItem((args.Performer, hands), out var markItem))
        {
            _popups.PopupClient(Loc.GetString("item-recall-item-mark-empty"), args.Performer, args.Performer);
            return;
        }

        if (HasComp<SoulBindedComponent>(markItem))
        {
            _popups.PopupClient(Loc.GetString("item-recall-item-already-marked", ("item", markItem)), args.Performer, args.Performer);
            return;
        }

        _popups.PopupClient(Loc.GetString("item-recall-item-marked", ("item", markItem.Value)), args.Performer, args.Performer);

        var bindeditem = AddComp<SoulBindedComponent>((EntityUid)markItem);

        bindeditem.Owner = args.Performer;
        Dirty((EntityUid)markItem, bindeditem);

        args.BindedItem = (EntityUid)markItem;
        args.BindSoulAction = args.Action;
        args.Handled = true;
    }

    private void OnComponentStartup(EntityUid uid, SoulBindedComponent component, ComponentInit args)
    {
        if (!TryComp<MetaDataComponent>(uid, out var metaData))
            return;

        _metaData.SetEntityName(uid, ("soul binded " + metaData.EntityName));
    }


}
