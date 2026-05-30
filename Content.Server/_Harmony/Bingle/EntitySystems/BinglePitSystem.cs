using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Shared._Harmony.Bingle.Components;
using Content.Shared._Harmony.Bingle.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Harmony.Bingle.EntitySystems;

public sealed class BinglePitSystem : SharedBinglePitSystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly ContainerSystem _containerSystem = default!;
    [Dependency] private readonly PolymorphSystem _polymorphSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly StunSystem _stunSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BinglePitComponent, DestructionEventArgs>(OnDestruction);
    }

    // The container system was complaining in shared so no prediction for thou
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BinglePitFallingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Fell ||
                _gameTiming.CurTime < comp.InsertIntoPitTime)
                continue;

            if (!comp.Pit.Valid ||
                !Exists(comp.Pit) ||
                !TryComp<BinglePitComponent>(comp.Pit, out var pit))
            {
                RemCompDeferred<BinglePitFallingComponent>(uid); // Pit doesn't exist anymore, remove the falling comp.
                continue;
            }

            _containerSystem.Insert(uid, pit.Pit);

            comp.Fell = true;
            Dirty(uid, comp);

            _stunSystem.TryKnockdown(uid, TimeSpan.FromSeconds(1), true); // Make them fall to the ground and drop anything they have.
        }
    }

    private void OnDestruction(Entity<BinglePitComponent> entity, ref DestructionEventArgs args)
    {
        foreach (var uid in _containerSystem.EmptyContainer(entity.Comp.Pit))
        {
            RemComp<BinglePitFallingComponent>(uid);
            _stunSystem.TryKnockdown(uid, entity.Comp.FallIntoPitTime, false);
        }

        var query = EntityQueryEnumerator<BinglePitFallingComponent>();
        while (query.MoveNext(out var fallingUid, out var fallingComp))
        {
            RemCompDeferred(fallingUid, fallingComp);
        }

        var ghostRoleQuery = EntityQueryEnumerator<BingleSpawnerComponent>();
        while (ghostRoleQuery.MoveNext(out var bingleUid, out var bingleComp))
        {
            if (bingleComp.Pit != entity.Owner ||
                TerminatingOrDeleted(bingleUid))
                return;

            QueueDel(bingleUid);
        }
    }

    protected override void PlayFallingAudio(Entity<BinglePitComponent> entity)
    {
        _audioSystem.PlayPvs(entity.Comp.FallingSound, entity);
    }

    protected override void UpgradeAllBingles(Entity<BinglePitComponent> entity)
    {
        var query = EntityQueryEnumerator<BingleComponent>();
        while (query.MoveNext(out var bingleUid, out var bingleComp))
        {
            if (bingleComp.Upgraded)
                continue;

            var polymorphable = EnsureComp<PolymorphableComponent>(bingleUid);
            _polymorphSystem.CreatePolymorphAction(bingleComp.UpgradePolymorph, (bingleUid, polymorphable));

            _popupSystem.PopupEntity(
                Loc.GetString("bingle-upgrade-success"),
                bingleUid,
                bingleUid,
                PopupType.Medium);
            bingleComp.Upgraded = true;
        }
    }
}
