using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Chat.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Harmony.Bingle.Components;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.PAI;
using Content.Shared.Physics;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Zombies;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Harmony.SafetyNet;

public sealed partial class SafetyNetSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;

    private Dictionary<EntityUid, TimeSpan> Queued = new();

    public float MaxSafetyVelocity = 30f;
    public float MaxDistance = 2000f;
    public float PlayerCheck = 12f;
    public float WallCheck = 3f;
    public TimeSpan SafetyNetInitialDelay = TimeSpan.FromMinutes(3);
    public TimeSpan SafetyNetDelay = TimeSpan.FromSeconds(45);
    public SoundSpecifier DefaultSafetyNetSfx = new SoundPathSpecifier("/Audio/Effects/space_wind.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FixturesComponent, EntParentChangedMessage>(OnGridChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(ClearCache);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_station.GetStations().Count == 0)
            return;

        var station = _robustRandom.Pick(_station.GetStations());
        if (_station.GetLargestGrid(station) is not { } grid)
            return;

        foreach (var (uid, time) in Queued)
        {
            if (!HasComp<TransformComponent>(uid))
            {
                Queued.Remove(uid); // Nope get OUT of the queue
                return;
            }

            if (_timing.CurTime < time)
                    continue;

            var queueTime = _timing.CurTime + SafetyNetDelay;

            var nearbyEntities = _entityLookup.GetEntitiesInRange<ActorComponent>(_transform.GetMapCoordinates(uid),
                PlayerCheck,
                LookupFlags.Uncontained);

            foreach (var ent in nearbyEntities)
            {
                if(HasComp<GhostComponent>(ent) || ent.Owner == uid)
                    nearbyEntities.Remove(ent);
            }

            if (nearbyEntities.Count > 0)
            {
                Queued.Remove(uid);
                Queued.Add(uid, queueTime);
                return;
            }

            var distance = (_physics.GetWorldAABB(grid).Center - _transform.GetWorldPosition(station) - _transform.GetWorldPosition(uid)).Length();

            if (distance > MaxDistance)
            {
                Queued.Remove(uid);
                Queued.Add(uid, queueTime);
                return;
            }

            var nearbyWalls = _entityLookup.GetEntitiesInRange<AirtightComponent>(_transform.GetMapCoordinates(uid), WallCheck, LookupFlags.StaticSundries);

            foreach (var wall in nearbyWalls)
            {
                if(TryComp<FixturesComponent>(wall.Owner, out var fixtures))
                {
                    if (fixtures.Fixtures.Values.First().CollisionLayer == (int)CollisionGroup.WallLayer)
                    {
                        Queued.Remove(uid);
                        Queued.Add(uid, queueTime); // in case what they were close to was a shuttle
                        return;
                    }
                }
                nearbyWalls.Remove(wall);
            }

            AttemptSafetyNet(uid);
            Queued.Remove(uid);
            Queued.Add(uid, queueTime); // in case they somehow fail to get any closer put them back in line
            return;
        }
    }

    private bool CanSafetyNet(EntityUid uid)
    {
        if (!HasComp<StealTargetComponent>(uid))
            return false;

        return true;
    }

    private void OnGridChanged(Entity<FixturesComponent> ent, ref EntParentChangedMessage args)
    {
        // Has to be here or just initialized entities will be on the list before they leave nullspace, this does mean anything spawned in space will NOT be considered by the SafetyNet.
        // I'm sure there's a better way to go about this but with my current knowledge this is the solution
        // Without this Aghost breaks
        if (args.OldMapId == null)
        {
            return;
        }

        if (args.Transform.GridUid == null && args.Transform.ParentUid == args.Transform.MapUid) // are you in space and not in a container
        {
            if (!CanSafetyNet(args.Entity))
                return;

            var queueTime = _timing.CurTime + SafetyNetInitialDelay;
            Queued.Add(args.Entity, queueTime);

        }
        else
        {
            if(Queued.ContainsKey(ent.Owner))
                Queued.Remove(ent.Owner);
        }
    }

    public void AttemptSafetyNet(EntityUid uid)
    {
        if (_station.GetStations().Count == 0)
            return;

        _audio.PlayPvs(DefaultSafetyNetSfx, uid);

        var station = _robustRandom.Pick(_station.GetStations());

        if (_station.GetLargestGrid(station) is not { } grid)
            return;

        var playableArea = _physics.GetWorldAABB(grid).Center;

        var rawvector = playableArea - _transform.GetMapCoordinates(station).Position - _transform.GetMapCoordinates(uid).Position;

        var vector = rawvector.Length() < MaxSafetyVelocity
                ? rawvector
                : rawvector.Normalized() * MaxSafetyVelocity;

        _physics.ApplyLinearImpulse(uid, vector);
    }

    private void ClearCache(RoundRestartCleanupEvent ev)
    {
        Queued.Clear();
    }
}
