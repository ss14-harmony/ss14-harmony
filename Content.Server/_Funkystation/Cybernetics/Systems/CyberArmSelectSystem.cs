using System.Linq;
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
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Cybernetics.Systems;

public sealed class CyberArmSelectSystem : EntitySystem
{
    [Dependency] private readonly SharedCyberArmStorageSystem _cyberArmStorage = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly ProtoId<OrganCategoryPrototype> ArmLeft = "ArmLeft";
    private static readonly ProtoId<OrganCategoryPrototype> ArmRight = "ArmRight";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandsComponent, EmptyHandActivateEvent>(OnEmptyHandActivateRef);
        Subs.BuiEvents<CyberLimbComponent>(CyberArmSelectUiKey.Key, sub => sub.Event<CyberArmSelectRequestMessage>(OnCyberArmSelectRequest));
    }

    private void OnEmptyHandActivateRef(Entity<HandsComponent> ent, ref EmptyHandActivateEvent ev)
    {
        if (ev.Handled)
            return;

        if (!ev.AltInteract)
            return;

        if (!HasComp<BodyComponent>(ev.User))
            return;

        // Determine which arm the activated hand belongs to - only show that arm's contents
        var armCategory = GetArmCategoryForHand(ev.User, ev.HandName, ent.Comp);
        // Exclude cyber modules and items that contain batteries but are not batteries themselves (e.g. flashlights with power cell slots)
        var items = _cyberArmStorage.GetCyberArmStorageItems(ev.User, armCategory)
            .Where(x => !HasComp<CyberLimbModuleComponent>(x.Item) && !(HasComp<PowerCellSlotComponent>(x.Item) && !HasComp<PowerCellComponent>(x.Item)))
            .ToList();
        if (items.Count == 0)
            return;

        var targetArm = items[0].Limb;
        if (!_ui.HasUi(targetArm, CyberArmSelectUiKey.Key))
            return;

        var state = new CyberArmSelectBoundUserInterfaceState(
            items.Select(x => new CyberArmSelectItemEntry(GetNetEntity(x.Item), Identity.Name(x.Item, EntityManager))).ToList());

        if (_ui.TryOpenUi(targetArm, CyberArmSelectUiKey.Key, ev.User))
        {
            _ui.SetUiState(targetArm, CyberArmSelectUiKey.Key, state);
            ev.Handled = true;
        }
    }

    /// <summary>
    /// Maps the activated hand to the corresponding arm category. Returns null to show all arms (fallback).
    /// </summary>
    private ProtoId<OrganCategoryPrototype>? GetArmCategoryForHand(EntityUid user, string? handName, HandsComponent handsComp)
    {
        var hand = handName;

        if (string.IsNullOrEmpty(hand) || !_hands.TryGetHand((user, handsComp), hand, out var handData))
            hand = handsComp.ActiveHandId;

        if (string.IsNullOrEmpty(hand) || !_hands.TryGetHand((user, handsComp), hand, out handData))
            return default;

        return handData.Value.Location switch
        {
            HandLocation.Left => ArmLeft,
            HandLocation.Right => ArmRight,
            HandLocation.Middle => ArmRight, // Middle hands typically map to right side
            _ => default
        };
    }

    private void OnCyberArmSelectRequest(Entity<CyberLimbComponent> ent, ref CyberArmSelectRequestMessage msg)
    {
        var user = msg.Actor;
        if (user == default)
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

        if (_virtualItem.TrySpawnVirtualItemInHand(selectedEntity.Value, user, out var virtualItem, false, null, false))
        {
            EnsureComp<CyberArmVirtualItemComponent>(virtualItem.Value);
            EnsureComp<UnremoveableComponent>(virtualItem.Value);
            _ui.CloseUi(ent.Owner, CyberArmSelectUiKey.Key, user);
        }
    }
}
