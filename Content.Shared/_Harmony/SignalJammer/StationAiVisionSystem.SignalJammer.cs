using System.Numerics;
using Content.Shared._Harmony.SignalJammer;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Shared.Map.Components;

namespace Content.Shared.Silicons.StationAi;

public sealed partial class StationAiVisionSystem
{
    [Dependency] private SharedSignalJammerSystem _signalJammer = default!;
    private readonly List<(Vector2 Position, float Range)> _signalFields = new();

    private bool IsSignalJammedCameraSeed(EntityUid seed)
    {
        return HasComp<SurveillanceCameraComponent>(seed)
            && _signalJammer.IsCovered(seed, SignalJammerEffect.Camera);
    }

    private bool IsSignalJammedTile(Entity<MapGridComponent> grid, Vector2i tile)
    {
        var local = _lookup.GetLocalBounds(tile, grid.Comp.TileSize).Center;
        var world = Vector2.Transform(local, _xforms.GetWorldMatrix(grid.Owner));
        return _signalJammer.IsCovered(world, Transform(grid.Owner).MapID, SignalJammerEffect.AiVision);
    }

    private void RemoveSignalJammedTiles(Entity<MapGridComponent> grid, HashSet<Vector2i> visibleTiles)
    {
        _signalJammer.GetFields(Transform(grid.Owner).MapID, SignalJammerEffect.AiVision, _signalFields);
        if (_signalFields.Count == 0)
            return;

        var matrix = _xforms.GetWorldMatrix(grid.Owner);
        visibleTiles.RemoveWhere(tile =>
        {
            var local = _lookup.GetLocalBounds(tile, grid.Comp.TileSize).Center;
            var world = Vector2.Transform(local, matrix);
            foreach (var (position, range) in _signalFields)
            {
                if (Vector2.DistanceSquared(world, position) <= range * range)
                    return true;
            }

            return false;
        });
    }
}
