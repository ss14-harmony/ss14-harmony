using Content.Shared.Body.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Pointing;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Body.Systems;

public sealed class BrainSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BrainComponent, OrganGotInsertedEvent>(OnBrainInserted);
        SubscribeLocalEvent<BrainComponent, OrganGotRemovedEvent>(OnBrainRemoved);
        SubscribeLocalEvent<BrainComponent, PointAttemptEvent>(OnPointAttempt);
    }

    private void OnBrainInserted(Entity<BrainComponent> brain, ref OrganGotInsertedEvent args)
    {
        HandleMind(args.Target, brain.Owner, brain.Owner, isInsert: true);
    }

    private void OnBrainRemoved(Entity<BrainComponent> brain, ref OrganGotRemovedEvent args)
    {
        HandleMind(brain.Owner, args.Target, brain.Owner, isInsert: false);
    }

    private void HandleMind(EntityUid newEntity, EntityUid oldEntity, EntityUid brain, bool isInsert)
    {
        if (_timing.ApplyingState)
            return;

        if (TerminatingOrDeleted(newEntity) || TerminatingOrDeleted(oldEntity))
            return;

        EnsureComp<MindContainerComponent>(newEntity);
        EnsureComp<MindContainerComponent>(oldEntity);

        var ghostOnMove = EnsureComp<GhostOnMoveComponent>(newEntity);
        ghostOnMove.MustBeDead = HasComp<MobStateComponent>(newEntity);

        if (!_mindSystem.TryGetMind(oldEntity, out var mindId, out var mind))
            return;

        if (_net.IsClient)
            return;

        _mindSystem.SetBrainEntity(mindId, brain, mind);

        var yank = isInsert && ShouldYankOnInsert(newEntity);
        _mindSystem.TransferTo(mindId, newEntity, ghostCheckOverride: yank, mind: mind);
    }

    private bool ShouldYankOnInsert(EntityUid target)
    {
        if (!TryComp<MobStateComponent>(target, out var mobState))
            return false;

        return _mobState.IsDead(target, mobState);
    }

    private void OnPointAttempt(Entity<BrainComponent> ent, ref PointAttemptEvent args)
    {
        args.Cancel();
    }
}
