using Content.Shared.Body.Systems; // Funky - CyberMed
using Content.Shared.Hands.EntitySystems;

namespace Content.Shared.Body;

public sealed class HandOrganSystem : EntitySystem
{
    [Dependency] private AppendageWearSlotSystem _appendageWearSlots = default!; // Funky - CyberMed
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandOrganComponent, OrganGotInsertedEvent>(OnGotInserted);
        SubscribeLocalEvent<HandOrganComponent, OrganGotRemovedEvent>(OnGotRemoved);
    }

    private void OnGotInserted(Entity<HandOrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        _hands.AddHand(args.Target, ent.Comp.HandID, ent.Comp.Data);
        _appendageWearSlots.RecomputeAppendageWearSlots(args.Target); // Funky - CyberMed
    }

    private void OnGotRemoved(Entity<HandOrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        // prevent a recursive double-delete bug
        if (LifeStage(args.Target) >= EntityLifeStage.Terminating)
            return;

        _hands.RemoveHand(args.Target, ent.Comp.HandID);
        _appendageWearSlots.RecomputeAppendageWearSlots(args.Target); // Funky - CyberMed
    }
}
