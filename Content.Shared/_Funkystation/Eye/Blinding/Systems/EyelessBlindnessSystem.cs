using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Eye.Blinding.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Eye.Blinding.Systems;

public sealed class EyelessBlindnessSystem : EntitySystem
{
    private static readonly ProtoId<OrganCategoryPrototype> EyesCategory = "Eyes";
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EyelessBlindnessComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EyelessBlindnessComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EyelessBlindnessComponent, CanSeeAttemptEvent>(OnBlindTrySee);

        SubscribeLocalEvent<BodyComponent, OrganInsertedIntoEvent>(OnOrganInsertedIntoBody);
        SubscribeLocalEvent<BodyComponent, OrganRemovedFromBodyNotifyEvent>(OnOrganRemovedFromBodyNotify);
    }

    private void OnStartup(Entity<EyelessBlindnessComponent> ent, ref ComponentStartup args)
    {
        _blindable.UpdateIsBlind(ent.Owner);
    }

    private void OnShutdown(Entity<EyelessBlindnessComponent> ent, ref ComponentShutdown args)
    {
        _blindable.UpdateIsBlind(ent.Owner);
    }

    private void OnBlindTrySee(EntityUid uid, EyelessBlindnessComponent component, CanSeeAttemptEvent args)
    {
        if (component.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }

    private void OnOrganInsertedIntoBody(Entity<BodyComponent> ent, ref OrganInsertedIntoEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (!TryComp<OrganComponent>(args.Organ, out var organ) || organ.Category != EyesCategory)
            return;

        if (!HasComp<EyelessBlindnessComponent>(ent.Owner))
            return;

        RemComp<EyelessBlindnessComponent>(ent.Owner);
    }

    private void OnOrganRemovedFromBodyNotify(Entity<BodyComponent> ent, ref OrganRemovedFromBodyNotifyEvent args)
    {
        if (_timing.ApplyingState)
            return;

        // Container removal during body deletion raises this event while the body is terminating;
        // we must not try to add components to an entity that is being deleted.
        if (TerminatingOrDeleted(ent.Owner))
            return;

        if (!TryComp<OrganComponent>(args.Organ, out var organ) || organ.Category != EyesCategory)
            return;

        // The removed organ may still appear in GetAllOrgans until container removal finishes.
        if (BodyHasAnyEyeOrgan(ent.Owner, args.Organ))
            return;

        EnsureComp<EyelessBlindnessComponent>(ent.Owner);
    }

    private bool BodyHasAnyEyeOrgan(EntityUid body, EntityUid? excludeOrgan = null)
    {
        foreach (var organUid in _body.GetAllOrgans(body))
        {
            if (excludeOrgan is { } ex && organUid == ex)
                continue;

            if (!TryComp<OrganComponent>(organUid, out var organ))
                continue;

            if (organ.Category == EyesCategory)
                return true;
        }

        return false;
    }
}
