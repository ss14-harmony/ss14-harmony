using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.Cybernetics.UI;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.PowerCell.Components;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Robust.Server.GameObjects;

namespace Content.Server.Cybernetics.Systems;

public sealed partial class CyberArmSelectSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private SharedCyberArmStorageSystem _cyberArmStorage = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandsComponent, EmptyHandActivateEvent>(OnEmptyHandActivateRef);
        SubscribeLocalEvent<CyberArmStorageActionComponent, OrganGotInsertedEvent>(OnStorageActionOrganInserted);
        SubscribeLocalEvent<CyberArmStorageActionComponent, OrganGotRemovedEvent>(OnStorageActionOrganRemoved);
        SubscribeLocalEvent<CyberArmStorageActionComponent, OpenCyberArmStorageActionEvent>(OnStorageActionPerformed);
        Subs.BuiEvents<CyberLimbComponent>(CyberArmSelectUiKey.Key, sub => sub.Event<CyberArmSelectRequestMessage>(OnCyberArmSelectRequest));
    }

    private void OnEmptyHandActivateRef(Entity<HandsComponent> ent, ref EmptyHandActivateEvent ev)
    {
        if (ev.Handled || !ev.AltInteract)
            return;

        if (!HasComp<BodyComponent>(ev.User))
            return;

        var handName = ev.HandName;
        if (string.IsNullOrEmpty(handName) || !_hands.TryGetHand((ev.User, ent.Comp), handName, out _))
            handName = ent.Comp.ActiveHandId;
        if (string.IsNullOrEmpty(handName))
            return;

        if (!_cyberArmStorage.TryGetCyberArmForHand(ev.User, handName, out var arm))
            return;

        if (TryOpenArmSelectUi(arm, ev.User))
            ev.Handled = true;
    }

    private void OnStorageActionOrganInserted(Entity<CyberArmStorageActionComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (!HasComp<ActionsComponent>(args.Target))
            return;

        _actions.AddAction(args.Target, ref ent.Comp.ActionEntity, ent.Comp.Action, ent.Owner);
    }

    private void OnStorageActionOrganRemoved(Entity<CyberArmStorageActionComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (LifeStage(args.Target) >= EntityLifeStage.Terminating)
            return;

        _actions.RemoveAction(args.Target, ent.Comp.ActionEntity);
        ent.Comp.ActionEntity = null;
    }

    private void OnStorageActionPerformed(Entity<CyberArmStorageActionComponent> ent, ref OpenCyberArmStorageActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<HandsComponent>(args.Performer, out var handsComp))
            return;

        if (!TryGetHandForArm(args.Performer, handsComp, ent.Owner, out var storageHand))
            return;

        if (!_hands.TrySetActiveHand((args.Performer, handsComp), storageHand))
            return;

        // Match alt-use behavior: pressing again while holding a cyber arm virtual item
        // first clears the currently active virtual item.
        if (_hands.TryGetActiveItem(args.Performer, out var held) &&
            TryComp<VirtualItemComponent>(held, out var heldVirtual) &&
            HasComp<CyberArmVirtualItemComponent>(held))
        {
            _virtualItem.DeleteVirtualItem((held.Value, heldVirtual), args.Performer);
        }

        if (TryOpenArmSelectUi(ent.Owner, args.Performer))
            args.Handled = true;
    }

    private bool TryGetHandForArm(EntityUid user, HandsComponent handsComp, EntityUid arm, out string handId)
    {
        foreach (var hand in handsComp.SortedHands)
        {
            if (_cyberArmStorage.TryGetCyberArmForHand(user, hand, out var handArm) && handArm == arm)
            {
                handId = hand;
                return true;
            }
        }

        handId = string.Empty;
        return false;
    }

    public bool TryOpenArmSelectUi(EntityUid arm, EntityUid user)
    {
        if (!TryComp<StorageComponent>(arm, out var storage) || storage.Container == null)
            return false;

        if (!_ui.HasUi(arm, CyberArmSelectUiKey.Key))
            return false;

        var items = storage.Container.ContainedEntities
            .Where(x => !HasComp<CyberLimbModuleComponent>(x) && !(HasComp<PowerCellSlotComponent>(x) && !HasComp<PowerCellComponent>(x)))
            .Select(x => new CyberArmSelectItemEntry(GetNetEntity(x), Identity.Name(x, EntityManager)))
            .ToList();

        if (items.Count == 0)
            return false;

        CloseOtherCyberArmUis(user, arm);

        if (!_ui.TryOpenUi(arm, CyberArmSelectUiKey.Key, user))
            return false;

        _ui.SetUiState(arm, CyberArmSelectUiKey.Key, new CyberArmSelectBoundUserInterfaceState(items));
        return true;
    }

    private void CloseOtherCyberArmUis(EntityUid user, EntityUid keepOpen)
    {
        foreach (var organ in _body.GetAllOrgans(user))
        {
            if (organ == keepOpen || !HasComp<CyberLimbComponent>(organ))
                continue;

            if (_ui.HasUi(organ, CyberArmSelectUiKey.Key))
                _ui.CloseUi(organ, CyberArmSelectUiKey.Key, user);
        }
    }

    private void OnCyberArmSelectRequest(Entity<CyberLimbComponent> ent, ref CyberArmSelectRequestMessage msg)
    {
        var user = msg.Actor;
        if (user == default)
            return;

        // Guardrail: never allow selection from an arm that doesn't match the active hand.
        if (!TryComp<HandsComponent>(user, out var handsComp))
            return;

        var activeHand = handsComp.ActiveHandId;
        if (string.IsNullOrEmpty(activeHand))
            return;

        if (!_cyberArmStorage.TryGetCyberArmForHand(user, activeHand, out var activeArm) || activeArm != ent.Owner)
            return;

        var selectedNet = msg.SelectedItem;
        if (!TryGetEntity(selectedNet, out var selectedEntity))
            return;

        // Only allow selecting items from this specific arm's storage
        var items = _cyberArmStorage.GetCyberArmStorageItems(user, null)
            .Where(x => x.Limb == ent.Owner && !HasComp<CyberLimbModuleComponent>(x.Item) && !(HasComp<PowerCellSlotComponent>(x.Item) && !HasComp<PowerCellComponent>(x.Item)))
            .ToList();
        if (!items.Any(x => x.Item == selectedEntity))
            return;

        if (_virtualItem.TrySpawnVirtualItemInHand(selectedEntity.Value, user, out var virtualItem, false, activeHand, false))
        {
            EnsureComp<CyberArmVirtualItemComponent>(virtualItem.Value);
            EnsureComp<UnremoveableComponent>(virtualItem.Value);
            _ui.CloseUi(ent.Owner, CyberArmSelectUiKey.Key, user);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("cyber-arm-storage-hand-full"), user, user, PopupType.SmallCaution);
        }
    }
}
