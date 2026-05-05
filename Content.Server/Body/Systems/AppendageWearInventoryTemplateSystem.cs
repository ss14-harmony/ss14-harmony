using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Inventory;

namespace Content.Server.Body.Systems;

/// <summary>
/// Server-only: inventory template swaps can add or remove glove/shoe slots after polymorph etc.
/// Handled on the server only so we do not duplicate the client's <see cref="InventoryTemplateUpdated"/> subscription.
/// </summary>
public sealed class AppendageWearInventoryTemplateSystem : EntitySystem
{
    [Dependency] private readonly AppendageWearSlotSystem _appendageWear = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, InventoryTemplateUpdated>(OnTemplateUpdated);
    }

    private void OnTemplateUpdated(EntityUid uid, InventoryComponent comp, ref InventoryTemplateUpdated args)
    {
        if (HasComp<BodyComponent>(uid))
            _appendageWear.RecomputeAppendageWearSlots(uid);
    }
}
