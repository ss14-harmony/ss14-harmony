using System.Collections.Generic;
using System.Linq;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Buckle.Components;
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

/// <summary>
/// Integer breakdown for UI preview (unsanitary surgery).
/// </summary>
public readonly record struct UnsanitaryPenaltyBreakdown(int Liquids, int NonSterileSurface, int RustyWalls, int Total);

public sealed class UnsanitarySurgeryCalculationSystem : EntitySystem
{
    private const float VoidPressureThreshold = 5000f; // 5 kPa - no bacteria in void
    private const int FloodFillMaxDistance = 3;
    private const string WaterReagentId = "Water";
    public const int NoSurgeryBedIntegrityPenalty = 2;
    private static readonly ProtoId<TagPrototype> RustyWallTag = "RustyWall";

    /// <summary>
    /// Strapped surgical fixtures that waive the no–surgery-bed integrity penalty.
    /// </summary>
    private static readonly HashSet<string> SurgeryBedPrototypeIds = new(StringComparer.Ordinal)
    {
        "MedicalBed",
        "OperatingTable",
        "StasisBed",
    };

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
        clearEv = new IntegrityPenaltyClearedEvent(ent.Owner, IntegrityPenaltyCategory.NoSurgeryBed);
        RaiseLocalEvent(ent.Owner, ref clearEv);

        var unsanitaryPenalty = CalculatePreview(ent.Owner).Total;
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

        if (!PatientOnSurgeryBed(ent.Owner))
        {
            var applyEv = new IntegrityPenaltyAppliedEvent(ent.Owner, NoSurgeryBedIntegrityPenalty,
                "health-analyzer-integrity-no-surgery-bed", IntegrityPenaltyCategory.NoSurgeryBed);
            RaiseLocalEvent(ent.Owner, ref applyEv);
        }
    }

    /// <summary>
    /// Whether the patient is strapped to a medical bed, operating table, or stasis bed (waives no-bed penalty).
    /// </summary>
    public bool PatientOnSurgeryBed(EntityUid patient)
    {
        if (!TryComp<BuckleComponent>(patient, out var buckle) || buckle.BuckledTo is not { } strapEnt)
            return false;

        var protoId = MetaData(strapEnt).EntityPrototype?.ID;
        return protoId != null && SurgeryBedPrototypeIds.Contains(protoId);
    }

    /// <summary>
    /// Preview unsanitary surgery penalty sources for the patient's current site (same logic as surgery completion).
    /// </summary>
    public UnsanitaryPenaltyBreakdown CalculatePreview(EntityUid patient)
    {
        if (!AccumulatePenaltyRawBySource(patient, out var liquidsF, out var nonSterileF, out var rustyF))
            return default;

        var sumF = liquidsF + nonSterileF + rustyF;
        var total = (int)System.Math.Ceiling(sumF);
        if (total <= 0)
            return default;

        AllocateSharesTotaling(total, liquidsF, nonSterileF, rustyF, sumF,
            out var liqInt, out var nsInt, out var rustInt);
        return new UnsanitaryPenaltyBreakdown(liqInt, nsInt, rustInt, total);
    }

    /// <summary>
    /// Accumulates liquid, non-sterile, and rusty-wall float contributions (same as legacy single-total path).
    /// </summary>
    private bool AccumulatePenaltyRawBySource(EntityUid patient,
        out float liquidsF, out float nonSterileF, out float rustyF)
    {
        liquidsF = 0f;
        nonSterileF = 0f;
        rustyF = 0f;

        if (!TryComp(patient, out TransformComponent? xform))
            return false;

        EntityUid gridUid;
        MapGridComponent grid;

        if (xform.GridUid is { } directGridUid && TryComp<MapGridComponent>(directGridUid, out var directGrid))
        {
            gridUid = directGridUid;
            grid = directGrid;
        }
        else if (!_mapManager.TryFindGridAt(_transform.GetMapCoordinates(patient), out var resolvedGridUid, out var resolvedGrid))
        {
            return false;
        }
        else
        {
            gridUid = resolvedGridUid;
            grid = resolvedGrid;
        }

        var mapCoords = _transform.GetMapCoordinates(patient);
        var startTile = _map.CoordinatesToTile(gridUid, grid, mapCoords);
        var floodedTiles = FloodFillAtmosphere(gridUid, grid, startTile);

        var rustyWallsCounted = new HashSet<EntityUid>();
        var puddleVolume = GetPuddleVolumeInRange(xform.Coordinates, range: 4f);

        foreach (var tile in floodedTiles)
        {
            var tileCoords = _map.GridTileToLocal(gridUid, grid, tile);
            var mixture = _atmosphere.GetTileMixture(gridUid, xform.MapUid, tile, excite: false);

            var liquidVolume = GetTileLiquidVolume(gridUid, grid, tile);
            if (liquidVolume == 0)
                liquidVolume = GetAnchoredPuddleVolume(gridUid, grid, tile);

            if (mixture == null || mixture.Pressure < VoidPressureThreshold)
            {
                if (liquidVolume > 0)
                {
                    liquidsF += (float)liquidVolume / 10f;
                    nonSterileF += 0.25f;
                }

                continue;
            }

            var isSterile = IsTileSterile(gridUid, grid, tile, tileCoords);
            var liquidPart = (float)liquidVolume / 10f;

            if (!isSterile)
            {
                liquidsF += liquidPart;
                nonSterileF += 0.25f;
            }
            else
            {
                liquidsF += liquidPart * 0.25f;
            }

            foreach (var adjDir in CardinalDirections)
            {
                var adjTile = tile.Offset(adjDir);
                foreach (var wall in GetRustyWallsInTile(gridUid, grid, adjTile))
                {
                    if (rustyWallsCounted.Add(wall))
                        rustyF += 1f;
                }
            }
        }

        if (puddleVolume > 0 && liquidsF + nonSterileF + rustyF == 0f)
        {
            liquidsF += (float)puddleVolume / 10f;
            nonSterileF += 0.25f;
        }

        return true;
    }

    /// <summary>
    /// Distribute <paramref name="total"/> across three buckets proportionally to raw floats; integers sum exactly to <paramref name="total"/>.
    /// </summary>
    private static void AllocateSharesTotaling(int total,
        float liquidsF, float nonSterileF, float rustyF, float sumF,
        out int liquids, out int nonSterile, out int rusty)
    {
        liquids = nonSterile = rusty = 0;
        if (total <= 0 || sumF <= 0f)
            return;

        var pl = total * (liquidsF / sumF);
        var pn = total * (nonSterileF / sumF);
        var pr = total * (rustyF / sumF);

        liquids = (int)System.Math.Floor(pl);
        nonSterile = (int)System.Math.Floor(pn);
        rusty = (int)System.Math.Floor(pr);

        var remainder = System.Math.Max(0, total - liquids - nonSterile - rusty);
        // Largest remainder method
        var fracs = new[] { pl - liquids, pn - nonSterile, pr - rusty };
        var order = new[] { 0, 1, 2 }.OrderByDescending(i => fracs[i]).ThenByDescending(i => i).ToArray();
        for (var i = 0; i < remainder; i++)
        {
            switch (order[i % order.Length])
            {
                case 0: liquids++; break;
                case 1: nonSterile++; break;
                default: rusty++; break;
            }
        }
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
