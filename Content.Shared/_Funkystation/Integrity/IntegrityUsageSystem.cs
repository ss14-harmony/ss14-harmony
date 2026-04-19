using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Integrity.Events;
using Robust.Shared.Timing;

namespace Content.Shared.Medical.Integrity;

public sealed class IntegrityUsageSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganComponent, OrganGotInsertedEvent>(OnOrganGotInserted);
        SubscribeLocalEvent<OrganComponent, OrganGotRemovedEvent>(OnOrganGotRemoved);
        SubscribeLocalEvent<OrganComponent, IntegrityCostRequestEvent>(OnIntegrityCostRequest);
    }

    private void OnOrganGotInserted(Entity<OrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        var body = args.Target;
        if (!Exists(body) || !TryComp<BodyComponent>(body, out _))
            return;

        var cost = ent.Comp.IntegrityCost;
        if (cost <= 0)
            return;

        var comp = EnsureComp<IntegrityUsageComponent>(body);
        comp.Usage += cost;
        Dirty(body, comp);
    }

    private void OnOrganGotRemoved(Entity<OrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        var body = args.Target;
        if (!Exists(body))
            return;

        var cost = ent.Comp.IntegrityCost;
        if (cost <= 0)
            return;

        if (!TryComp<IntegrityUsageComponent>(body, out var comp))
            return;

        comp.Usage = System.Math.Max(0, comp.Usage - cost);
        if (comp.Usage <= 0)
            RemComp<IntegrityUsageComponent>(body);
        else
            Dirty(body, comp);
    }

    private void OnIntegrityCostRequest(Entity<OrganComponent> ent, ref IntegrityCostRequestEvent args)
    {
        if (args.Organ != ent.Owner)
            return;

        args.Cost = ent.Comp.IntegrityCost;
    }
}
