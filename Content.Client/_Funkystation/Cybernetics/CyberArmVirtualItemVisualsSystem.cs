using Content.Client.Items.Systems;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Hands;
using Content.Shared.Inventory.VirtualItem;

namespace Content.Client.Cybernetics;

/// <summary>
/// Redirects GetInhandVisualsEvent from cyber arm virtual items to the blocking entity
/// so the real item's in-hand sprites are displayed.
/// </summary>
public sealed class CyberArmVirtualItemVisualsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberArmVirtualItemComponent, GetInhandVisualsEvent>(OnGetVisuals,
            before: [typeof(ItemSystem)]);
    }

    private void OnGetVisuals(EntityUid uid, CyberArmVirtualItemComponent component, GetInhandVisualsEvent args)
    {
        if (!TryComp<VirtualItemComponent>(uid, out var virt) || !Exists(virt.BlockingEntity))
            return;

        args.Layers.Clear();
        RaiseLocalEvent(virt.BlockingEntity, args);
    }
}
