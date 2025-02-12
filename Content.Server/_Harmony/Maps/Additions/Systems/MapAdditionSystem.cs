using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Harmony.Maps.Additions.Systems;

public sealed class MapAdditionSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
    }

    private void OnPostGameMapLoad(PostGameMapLoad args)
    {
        foreach (var mapAddition in _prototypeManager.EnumeratePrototypes<MapAdditionPrototype>())
        {
            if (mapAddition.ApplyOn != args.GameMap.ID)
                continue;

            ApplyMapAddition(mapAddition, args.GameMap, args.Map);
        }
    }

    /// <summary>
    /// Apply a map modification to a map
    /// </summary>
    /// <remarks>
    /// Ignores the <see cref="MapAdditionPrototype.ApplyOn"/> field
    /// </remarks>
    public void ApplyMapAddition(MapAdditionPrototype mapAddition, GameMapPrototype gameMap, MapId map)
    {
        Log.Debug("Applying map addition {0} to map {1}", mapAddition.ID, gameMap.ID);

        // Find the station, we are unable to use the station system because our map is still paused.
        var stationQuery = EntityQueryEnumerator<TransformComponent, BecomesStationComponent>();
        EntityUid? station = null;

        while (stationQuery.MoveNext(out var uid, out var transform, out var _))
        {
            if (transform.MapID != map)
                return;

            station = uid;
            break;
        }

        if (station == null)
        {
            Log.Error("Tried to apply map addition {0} to map {1} but failed to find a station!", mapAddition.ID, gameMap.ID);
            return;
        }

        foreach (var entityAddition in mapAddition.Entities)
        {
            var entity = _entityManager.CreateEntityUninitialized(entityAddition.Prototype,
                new EntityCoordinates(station.Value, entityAddition.Position),
                entityAddition.Components,
                entityAddition.Rotation);

            _entityManager.InitializeAndStartEntity(entity, false);
        }
    }
}
