using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Shared._Harmony.EntitySelector.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Harmony.Maps.Modifications.Systems;

public sealed class MapModificationSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly EntitySelectorSystem _entitySelectorSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
    }

    private void OnPostGameMapLoad(PostGameMapLoad args)
    {
        foreach (var mapModification in _prototypeManager.EnumeratePrototypes<MapModificationPrototype>())
        {
            if (!mapModification.ApplyOn.Contains(args.GameMap.ID))
                continue;

            Log.Debug("Applying map addition {0} to map {1}", mapModification.ID, args.GameMap.ID);

            ApplyMapModification(mapModification, args.Map);
        }
    }

    /// <summary>
    /// Apply a map modification to a map
    /// </summary>
    /// <remarks>
    /// Assumes an uninitialized map
    /// </remarks>
    public void ApplyMapModification(MapModificationPrototype mapModification, MapId map)
    {
        // Query all entities with the becomes station component and pick the first one in our map.
        // We have to use the becomes station component because our map might be uninitialized.
        var stationQuery = EntityQueryEnumerator<TransformComponent, BecomesStationComponent>();
        EntityUid? station = null;
        while (stationQuery.MoveNext(out var uid, out var transform, out _))
        {
            if (transform.MapID != map)
                return;

            station = uid;
            break;
        }

        if (station == null)
        {
            Log.Error("Tried to apply map modification {0} to map {1} but failed to find a station!", mapModification.ID, map);
            return;
        }

        var entitiesToAdd = new List<MapModificationEntity>();
        entitiesToAdd.AddRange(mapModification.Additions);

        // Iterate over all entities inside the station grid
        var removalEntityEnumerator = Transform(station.Value).ChildEnumerator;
        while (removalEntityEnumerator.MoveNext(out var entity))
        {
            // Apply removals
            if (_entitySelectorSystem.EntityMatchesAny(entity, mapModification.Removals))
            {
                Del(entity);
                continue;
            }

            // Apply replacements
            foreach (var replacement in mapModification.Replacements)
            {
                if (!_entitySelectorSystem.EntityMatchesAny(entity, replacement.From))
                    continue;

                var entityTransform = Transform(entity);
                var newEntity = new MapModificationEntity
                {
                    Prototype = replacement.NewPrototype,
                    Name = replacement.NewName,
                    Description = replacement.NewDescription,
                    Position = entityTransform.LocalPosition,
                    Rotation = replacement.NewRotation ?? entityTransform.LocalRotation,
                    Components = replacement.NewComponents,
                };

                Del(entity);
                entitiesToAdd.Add(newEntity);
            }
        }

        // Apply additions
        foreach (var addition in entitiesToAdd)
        {
            ApplyMapModificationEntity(addition, station.Value);
        }
    }

    private void ApplyMapModificationEntity(MapModificationEntity newEntity, EntityUid grid)
    {
        var entity = _entityManager.CreateEntityUninitialized(newEntity.Prototype,
            new EntityCoordinates(grid, newEntity.Position),
            newEntity.Components,
            newEntity.Rotation ?? default);

        _entityManager.InitializeAndStartEntity(entity, false);

        if (newEntity.Name != null)
            _metaDataSystem.SetEntityName(entity, newEntity.Name);

        if (newEntity.Description != null)
            _metaDataSystem.SetEntityDescription(entity, newEntity.Description);
    }
}
