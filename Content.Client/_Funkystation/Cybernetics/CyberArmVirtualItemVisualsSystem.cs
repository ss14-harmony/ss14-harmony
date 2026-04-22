using Content.Client.Items.Systems;
using Content.Shared.Body;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Storage;

namespace Content.Client.Cybernetics;

/// <summary>
/// Redirects GetInhandVisualsEvent from cyber arm virtual items to the blocking entity
/// so the real item's in-hand sprites are displayed, and forwards VisualsChangedEvent
/// from items in cyber arm storage to the player's hand so in-hand sprites refresh
/// when the stored item's visual state changes (e.g. toggling a lighter or energy sword).
/// </summary>
public sealed class CyberArmVirtualItemVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberArmVirtualItemComponent, GetInhandVisualsEvent>(OnGetVisuals,
            before: [typeof(ItemSystem)]);
        SubscribeLocalEvent<CyberLimbComponent, VisualsChangedEvent>(OnCyberLimbVisualsChanged);
    }

    private void OnGetVisuals(EntityUid uid, CyberArmVirtualItemComponent component, GetInhandVisualsEvent args)
    {
        if (!TryComp<VirtualItemComponent>(uid, out var virt) || !Exists(virt.BlockingEntity))
            return;

        args.Layers.Clear();
        RaiseLocalEvent(virt.BlockingEntity, args);
    }

    /// <summary>
    /// When an item inside a cyber limb's storage has its visuals change, the engine raises
    /// VisualsChangedEvent on the cyber limb (the storage's owner). The player's hand
    /// visuals won't refresh from that because the hand actually holds a virtual item, not
    /// the real item. Forward the event to the body's hand that holds the matching virtual
    /// item so its sprite gets rebuilt via the usual pipeline.
    /// </summary>
    private void OnCyberLimbVisualsChanged(EntityUid uid, CyberLimbComponent component, VisualsChangedEvent args)
    {
        if (args.ContainerId != StorageComponent.ContainerId)
            return;

        if (!TryComp<OrganComponent>(uid, out var organ) || organ.Body is not { } body)
            return;

        if (!TryComp<HandsComponent>(body, out var hands))
            return;

        var item = GetEntity(args.Item);

        foreach (var handId in hands.Hands.Keys)
        {
            if (_hands.GetHeldItem((body, hands), handId) is not { } held)
                continue;

            if (!HasComp<CyberArmVirtualItemComponent>(held))
                continue;

            if (!TryComp<VirtualItemComponent>(held, out var virt) || virt.BlockingEntity != item)
                continue;

            RaiseLocalEvent(body, new VisualsChangedEvent(GetNetEntity(held), handId));
            break;
        }
    }
}
