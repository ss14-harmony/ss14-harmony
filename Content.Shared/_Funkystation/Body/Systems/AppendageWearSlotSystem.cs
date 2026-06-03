using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body.Systems;

/// <summary>
/// Drops gloves or shoes when the body has no eligible hand or foot organs, and blocks equipping into those slots.
/// </summary>
public sealed class AppendageWearSlotSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private INetManager _net = default!;

    private static readonly ProtoId<OrganCategoryPrototype>[] HandCategories =
    [
        new("HandLeft"),
        new("HandRight"),
    ];

    private static readonly ProtoId<OrganCategoryPrototype>[] FootCategories =
    [
        new("FootLeft"),
        new("FootRight"),
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandsComponent, HandCountChangedEvent>(OnHandCountChanged);
        SubscribeLocalEvent<BodyComponent, MapInitEvent>(OnBodyMapInit);
        SubscribeLocalEvent<BodyComponent, AppendageWearInventoryRefreshEvent>(OnAppendageWearInventoryRefresh);
        // Hand physiology: HandOrganSystem calls Recompute after AddHand/RemoveHand (only one subscriber allowed on HandOrgan + organ events).
        SubscribeLocalEvent<BodyComponent, IsEquippingTargetAttemptEvent>(OnIsEquippingTargetAttempt);
        SubscribeLocalEvent<ItemComponent, BeingEquippedAttemptEvent>(OnItemBeingEquipped);
    }

    private void OnHandCountChanged(EntityUid uid, HandsComponent comp, HandCountChangedEvent args)
    {
        RecomputeAppendageWearSlots(uid);
    }

    private void OnBodyMapInit(EntityUid uid, BodyComponent comp, MapInitEvent args)
    {
        RecomputeAppendageWearSlots(uid);
    }

    private void OnAppendageWearInventoryRefresh(Entity<BodyComponent> ent, ref AppendageWearInventoryRefreshEvent args)
    {
        RecomputeAppendageWearSlots(ent.Owner);
    }

    private void OnIsEquippingTargetAttempt(Entity<BodyComponent> ent, ref IsEquippingTargetAttemptEvent args)
    {
        TryBlockAppendageEquip(ent.Owner, args);
    }

    private void OnItemBeingEquipped(Entity<ItemComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<BodyComponent>(args.EquipTarget))
            return;

        TryBlockAppendageEquip(args.EquipTarget, args);
    }

    /// <summary>
    /// Shared rules for blocking glove/footwear slots; used for both target and item equip-attempt events.
    /// </summary>
    private void TryBlockAppendageEquip(EntityUid wearer, EquipAttemptBase args)
    {
        if (args.Cancelled)
            return;

        if ((args.SlotFlags & SlotFlags.GLOVES) != 0 && !HasEligibleHandOrgan(wearer))
        {
            args.Reason = "appendage-wear-slot-gloves-no-hands";
            args.Cancel();
            return;
        }

        if ((args.SlotFlags & SlotFlags.FEET) != 0 && !HasEligibleFootOrgan(wearer))
        {
            args.Reason = "appendage-wear-slot-shoes-no-feet";
            args.Cancel();
        }
    }

    public bool HasEligibleHandOrgan(EntityUid body)
    {
        if (!HasComp<BodyComponent>(body))
            return false;

        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (!TryComp<OrganComponent>(organ, out var organComp) || organComp.Category is not { } cat)
                continue;

            if (!CategoryIn(cat, HandCategories))
                continue;

            if (HasComp<BlocksHandWearSlotComponent>(organ))
                continue;

            return true;
        }

        return false;
    }

    public bool HasEligibleFootOrgan(EntityUid body)
    {
        if (!HasComp<BodyComponent>(body))
            return false;

        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (!TryComp<OrganComponent>(organ, out var organComp) || organComp.Category is not { } cat)
                continue;

            if (!CategoryIn(cat, FootCategories))
                continue;

            if (HasComp<BlocksFootWearSlotComponent>(organ))
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Force-unequips gloves or shoes if the body is ineligible. Safe to call repeatedly.
    /// Server-authoritative: clients skip actual unequip (see <see cref="BrainSystem"/>).
    /// </summary>
    public void RecomputeAppendageWearSlots(EntityUid body)
    {
        if (TerminatingOrDeleted(body))
            return;

        if (!HasComp<InventoryComponent>(body) || !HasComp<BodyComponent>(body))
            return;

        // Mutations (TryUnequip) only happen server-side, mirroring BrainSystem.
        if (_net.IsClient)
            return;

        if (_inventory.HasSlot(body, "gloves")
            && _inventory.TryGetSlotEntity(body, "gloves", out _)
            && !HasEligibleHandOrgan(body))
        {
            _inventory.TryUnequip(body, "gloves",
                silent: true, force: true, predicted: false,
                checkDoafter: false, triggerHandContact: false);
        }

        if (_inventory.HasSlot(body, "shoes")
            && _inventory.TryGetSlotEntity(body, "shoes", out _)
            && !HasEligibleFootOrgan(body))
        {
            _inventory.TryUnequip(body, "shoes",
                silent: true, force: true, predicted: false,
                checkDoafter: false, triggerHandContact: false);
        }
    }

    private static bool CategoryIn(ProtoId<OrganCategoryPrototype> cat, ProtoId<OrganCategoryPrototype>[] set)
    {
        foreach (var id in set)
        {
            if (id == cat)
                return true;
        }

        return false;
    }
}
