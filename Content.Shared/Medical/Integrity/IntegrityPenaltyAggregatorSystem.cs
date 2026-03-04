using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Integrity.Events;

namespace Content.Shared.Medical.Integrity;

public sealed class IntegrityPenaltyAggregatorSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;

    private EntityQuery<IntegrityPenaltyComponent> _penaltyQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganComponent, SurgeryPenaltyAppliedEvent>(OnSurgeryPenaltyApplied);
        SubscribeLocalEvent<OrganComponent, SurgeryPenaltyRemovedEvent>(OnSurgeryPenaltyRemoved);
        SubscribeLocalEvent<BodyComponent, IntegrityPenaltyAppliedEvent>(OnIntegrityPenaltyApplied);
        SubscribeLocalEvent<BodyComponent, IntegrityPenaltyClearedEvent>(OnIntegrityPenaltyCleared);
        SubscribeLocalEvent<BodyComponent, IntegrityPenaltyTotalRequestEvent>(OnIntegrityPenaltyTotalRequest);

        _penaltyQuery = GetEntityQuery<IntegrityPenaltyComponent>();
    }

    private void OnSurgeryPenaltyApplied(Entity<OrganComponent> ent, ref SurgeryPenaltyAppliedEvent args)
    {
        if (args.BodyPart != ent.Owner)
            return;

        var comp = EnsureComp<IntegrityPenaltyComponent>(ent);
        comp.Penalty += args.Amount;
        Dirty(ent, comp);
    }

    private void OnSurgeryPenaltyRemoved(Entity<OrganComponent> ent, ref SurgeryPenaltyRemovedEvent args)
    {
        if (args.BodyPart != ent.Owner)
            return;

        if (!TryComp<IntegrityPenaltyComponent>(ent, out var comp))
            return;

        comp.Penalty = System.Math.Max(0, comp.Penalty - args.Amount);
        if (comp.Penalty <= 0)
            RemComp<IntegrityPenaltyComponent>(ent);
        else
            Dirty(ent, comp);
    }

    private void OnIntegrityPenaltyApplied(Entity<BodyComponent> ent, ref IntegrityPenaltyAppliedEvent args)
    {
        if (args.Body != ent.Owner)
            return;

        var comp = EnsureComp<IntegritySurgeryComponent>(ent);
        comp.Entries.Add(new IntegrityPenaltyEntry(args.Reason, args.Category, args.Amount, args.Children));
        Dirty(ent, comp);
    }

    private void OnIntegrityPenaltyCleared(Entity<BodyComponent> ent, ref IntegrityPenaltyClearedEvent args)
    {
        if (args.Body != ent.Owner)
            return;

        if (!TryComp<IntegritySurgeryComponent>(ent, out var comp))
            return;

        var category = args.Category;
        comp.Entries.RemoveAll(e => e.Category == category);
        if (comp.Entries.Count == 0)
            RemComp<IntegritySurgeryComponent>(ent);
        else
            Dirty(ent, comp);
    }

    private void OnIntegrityPenaltyTotalRequest(Entity<BodyComponent> ent, ref IntegrityPenaltyTotalRequestEvent args)
    {
        if (args.Body != ent.Owner)
            return;

        var total = 0;

        foreach (var organ in _body.GetAllOrgans(ent.Owner))
        {
            if (_penaltyQuery.TryComp(organ, out var penalty))
                total += penalty.Penalty;
        }

        if (TryComp<IntegritySurgeryComponent>(ent, out var surgeryComp))
        {
            foreach (var entry in surgeryComp.Entries)
                total += SumEntryRecursive(entry);
        }

        args.Total = total;
    }

    private static int SumEntryRecursive(IntegrityPenaltyEntry entry)
    {
        if (entry.Children != null && entry.Children.Count > 0)
            return entry.Amount;
        return entry.Amount;
    }
}
