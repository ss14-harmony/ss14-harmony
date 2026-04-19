using System.Collections.Generic;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Maps;
using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Integrity.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Shared.Tag;
using Content.Server.Atmos.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.Integrity;

public sealed class UnsanitarySurgeryCalculationSystem : EntitySystem
{
    private const float VoidPressureThreshold = 5000f; // 5 kPa - no bacteria in void
    private const int FloodFillMaxDistance = 3;
    private const string WaterReagentId = "Water";
    private static readonly ProtoId<TagPrototype> RustyWallTag = "RustyWall";

    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly AtmosDirection[] CardinalDirections =
    [
        AtmosDirection.North,
        AtmosDirection.South,
        AtmosDirection.East,
        AtmosDirection.West
    ];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, UnsanitarySurgeryPenaltyRequestEvent>(OnUnsanitarySurgeryPenaltyRequest);
    }

    private void OnUnsanitarySurgeryPenaltyRequest(Entity<BodyComponent> ent, ref UnsanitarySurgeryPenaltyRequestEvent args)
    {
        var clearEv = new IntegrityPenaltyClearedEvent(ent.Owner, IntegrityPenaltyCategory.UnsanitarySurgery);
        RaiseLocalEvent(ent.Owner, ref clearEv);
        clearEv = new IntegrityPenaltyClearedEvent(ent.Owner, IntegrityPenaltyCategory.ImproperTools);
        RaiseLocalEvent(ent.Owner, ref clearEv);

        var unsanitaryPenalty = CalculateUnsanitaryPenalty(ent.Owner);
        if (unsanitaryPenalty > 0)
        {
            var applyEv = new IntegrityPenaltyAppliedEvent(ent.Owner, unsanitaryPenalty, "health-analyzer-integrity-unsanitary-surgery", IntegrityPenaltyCategory.UnsanitarySurgery);
            RaiseLocalEvent(ent.Owner, ref applyEv);
        }

        if (args.IsImprovised)
        {
            var bodyPartName = TryComp<OrganComponent>(args.BodyPart, out var organComp) && organComp.Category is { } cat
                ? cat.ToString()
                : Identity.Name(args.BodyPart, EntityManager);
            var stepName = args.Procedure?.Name ?? args.Step?.Name?.Id ?? args.StepId;
            var improvisedAmount = 1;
            var stepAmount = args.Procedure?.Penalty ?? args.Step?.Penalty ?? 0;
            var improvisedChild = new IntegrityPenaltyEntry("health-analyzer-integrity-improvised-tool", IntegrityPenaltyCategory.ImproperTools, improvisedAmount, null);
            var stepEntry = new IntegrityPenaltyEntry(stepName, IntegrityPenaltyCategory.ImproperTools, stepAmount, new List<IntegrityPenaltyEntry> { improvisedChild });
            var children = new List<IntegrityPenaltyEntry> { stepEntry };
            var totalAmount = stepAmount + improvisedAmount;
            var applyEv = new IntegrityPenaltyAppliedEvent(ent.Owner, totalAmount, bodyPartName ?? "?", IntegrityPenaltyCategory.ImproperTools, children);
            RaiseLocalEvent(ent.Owner, ref applyEv);
        }
    }

    private int CalculateUnsanitaryPenalty(EntityUid patient)
    {
        if (!TryComp(patient, out TransformComponent? xform))
            return 0;

        EntityUid gridUid;
        MapGridComponent grid;

        // Patient may have null GridUid when parented to non-grid entity (e.g. buckled to bed).
        // Fallback: resolve grid from patient's world position.
        if (xform.GridUid is { } directGridUid && TryComp<MapGridComponent>(directGridUid, out var directGrid))
        {
            gridUid = directGridUid;
            grid = directGrid;
        }
        else if (!_mapManager.TryFindGridAt(_transform.GetMapCoordinates(patient), out var resolvedGridUid, out var resolvedGrid))
        {
            return 0;
        }
        else
        {
            gridUid = resolvedGridUid;
            grid = resolvedGrid;
        }

        var mapCoords = _transform.GetMapCoordinates(patient);
        var startTile = _map.CoordinatesToTile(gridUid, grid, mapCoords);
        var floodedTiles = FloodFillAtmosphere(gridUid, grid, startTile);

        var totalPenalty = 0f;
        var rustyWallsCounted = new HashSet<EntityUid>();

        // Get puddles in range of patient - GetEntitiesInRange<PuddleComponent> works reliably
        // (used by DrainSystem, JuggernautBloodAbsorption). Range 4 covers ~3 tiles.
        var puddleVolume = GetPuddleVolumeInRange(xform.Coordinates, range: 4f);

        foreach (var tile in floodedTiles)
        {
            var tileCoords = _map.GridTileToLocal(gridUid, grid, tile);
            var mixture = _atmosphere.GetTileMixture(gridUid, xform.MapUid, tile, excite: false);

            // Always check for puddles - GetAnchoredEntities is used by PuddleSystem itself when spilling
            var liquidVolume = GetTileLiquidVolume(gridUid, grid, tile);
            if (liquidVolume == 0)
                liquidVolume = GetAnchoredPuddleVolume(gridUid, grid, tile);

            if (mixture == null || mixture.Pressure < VoidPressureThreshold)
            {
                // No atmosphere - still count liquid as unsanitary
                if (liquidVolume > 0)
                    totalPenalty += (float)liquidVolume / 10f + 0.25f;
                continue;
            }

            var tilePenalty = 0f;
            var isSterile = IsTileSterile(gridUid, grid, tile, tileCoords);

            tilePenalty += (float)liquidVolume / 10f;

            if (!isSterile)
                tilePenalty += 0.25f;
            else
                tilePenalty *= 0.25f;

            totalPenalty += tilePenalty;

            foreach (var adjDir in CardinalDirections)
            {
                var adjTile = tile.Offset(adjDir);
                foreach (var wall in GetRustyWallsInTile(gridUid, grid, adjTile))
                {
                    if (rustyWallsCounted.Add(wall))
                        totalPenalty += 1f;
                }
            }
        }

        // Fallback: range-based puddle detection when tile-based logic missed it (e.g. empty map, no atmosphere)
        if (puddleVolume > 0 && totalPenalty == 0)
            totalPenalty = (float)puddleVolume / 10f + 0.25f;

        return (int)System.Math.Ceiling(totalPenalty);
    }

    private HashSet<Vector2i> FloodFillAtmosphere(EntityUid gridUid, MapGridComponent grid, Vector2i start)
    {
        var result = new HashSet<Vector2i> { start };
        var queue = new Queue<(Vector2i pos, int depth)>();
        queue.Enqueue((start, 0));

        while (queue.Count > 0)
        {
            var (pos, depth) = queue.Dequeue();
            if (depth >= FloodFillMaxDistance)
                continue;

            foreach (var dir in CardinalDirections)
            {
                if (_atmosphere.IsTileAirBlockedCached((gridUid, null), pos, dir))
                    continue;

                var next = pos.Offset(dir);
                if (!result.Add(next))
                    continue;

                queue.Enqueue((next, depth + 1));
            }
        }

        return result;
    }

    private bool IsTileSterile(EntityUid gridUid, MapGridComponent grid, Vector2i indices, EntityCoordinates coords)
    {
        if (_turf.TryGetTileRef(coords, out var tileRef))
        {
            var tileDef = _turf.GetContentTileDefinition(tileRef.Value);
            if (tileDef.SterileSurgerySurface)
                return true;
        }

        foreach (var uid in _map.GetAnchoredEntities(gridUid, grid, indices))
        {
            if (HasComp<SterileSurgerySurfaceComponent>(uid))
                return true;
        }

        return false;
    }

    private FixedPoint2 GetPuddleVolumeInRange(EntityCoordinates coords, float range)
    {
        var puddles = _lookup.GetEntitiesInRange<PuddleComponent>(coords, range);
        var total = FixedPoint2.Zero;
        foreach (var puddle in puddles)
            total += GetUnsanitaryPuddleVolume(puddle.Owner, puddle.Comp);
        return total;
    }

    /// <summary>
    /// Volume of puddle reagents that count as unsanitary. Water is excluded (clean water is not unsanitary).
    /// </summary>
    private FixedPoint2 GetUnsanitaryPuddleVolume(EntityUid uid, PuddleComponent puddle)
    {
        if (!_solutionContainer.TryGetSolution(uid, puddle.SolutionName, out _, out var solution))
            return FixedPoint2.Zero;

        var total = FixedPoint2.Zero;
        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (reagent.Prototype != WaterReagentId)
                total += quantity;
        }
        return total;
    }

    private FixedPoint2 GetTileLiquidVolume(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        // Use GetLocalEntitiesIntersecting (same as CleanTileReaction/cleannades) - puddles have Physics
        // so they're found via broadphase.
        if (!_map.TryGetTileRef(gridUid, grid, indices, out var tileRef))
            return FixedPoint2.Zero;

        var total = FixedPoint2.Zero;
        foreach (var uid in _lookup.GetLocalEntitiesIntersecting(tileRef, 0f))
        {
            if (TryComp<PuddleComponent>(uid, out var puddle))
                total += GetUnsanitaryPuddleVolume(uid, puddle);
        }
        return total;
    }

    /// <summary>
    /// Puddle volume via GetAnchoredEntities - same approach PuddleSystem uses when finding puddles to add to.
    /// Use when GetLocalEntitiesIntersecting returns 0 (e.g. empty map with no physics broadphase).
    /// </summary>
    private FixedPoint2 GetAnchoredPuddleVolume(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        var total = FixedPoint2.Zero;
        foreach (var uid in _map.GetAnchoredEntities(gridUid, grid, indices))
        {
            if (TryComp<PuddleComponent>(uid, out var puddle))
                total += GetUnsanitaryPuddleVolume(uid, puddle);
        }
        return total;
    }

    private IEnumerable<EntityUid> GetRustyWallsInTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        foreach (var uid in _map.GetAnchoredEntities(gridUid, grid, indices))
        {
            if (_tag.HasTag(uid, RustyWallTag))
                yield return uid;
        }
    }
}
