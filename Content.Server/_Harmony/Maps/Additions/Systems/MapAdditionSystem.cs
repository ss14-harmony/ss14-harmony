using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Harmony.Maps.Additions.Systems;

public sealed class MapAdditionSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
    }

    private void OnPostGameMapLoad(PostGameMapLoad args)
    {
        foreach (var mapAddition in _prototypeManager.EnumeratePrototypes<MapAdditionPrototype>())
        {
            if (mapAddition.ApplyOn == null || mapAddition.ApplyOn != args.GameMap.ID)
                continue;

            Log.Debug("Applying map addition {0} to map {1}", mapAddition.ID, args.GameMap.ID);

            ApplyMapAddition(mapAddition, args.Map);
        }
    }

    /// <summary>
    /// Apply a map modification to a map
    /// </summary>
    /// <remarks>
    /// Assumes an uninitialized map
    /// </remarks>
    public void ApplyMapAddition(MapAdditionPrototype mapAddition, MapId map)
    {
        // Query all entities with the becomes station component and pick the first one in our map.
        // We have to use the becomes station component because our map might be uninitialized.
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
            Log.Error("Tried to apply map addition {0} to map {1} but failed to find a station!", mapAddition.ID, map);
            return;
        }

        foreach (var entityAddition in mapAddition.Entities)
        {
            var entity = _entityManager.CreateEntityUninitialized(entityAddition.Prototype,
                new EntityCoordinates(station.Value, entityAddition.Position),
                entityAddition.Components,
                entityAddition.Rotation ?? default);

            _entityManager.InitializeAndStartEntity(entity, false);

            if (entityAddition.Name != null)
                _metaDataSystem.SetEntityName(entity, entityAddition.Name);

            if (entityAddition.Description != null)
                _metaDataSystem.SetEntityDescription(entity, entityAddition.Description);
        }
    }
}
