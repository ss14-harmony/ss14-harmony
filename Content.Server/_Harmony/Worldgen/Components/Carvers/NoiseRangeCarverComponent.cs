using System.Numerics;
using Content.Server._Harmony.Worldgen.Prototypes;
using Content.Server._Harmony.Worldgen.Systems.Carvers;
using Robust.Shared.Prototypes;

namespace Content.Server._Harmony.Worldgen.Components.Carvers;

/// <summary>
///     This is used for carving out empty space in the game world, providing byways through the debris field.
/// </summary>
[RegisterComponent]
[Access(typeof(NoiseRangeCarverSystem))]
public sealed partial class NoiseRangeCarverComponent : Component
{
    /// <summary>
    ///     The noise channel to use as a density controller.
    /// </summary>
    /// <remarks>This noise channel should be mapped to exactly the range [0, 1] unless you want a lot of warnings in the log.</remarks>
    [DataField("noiseChannel")]
    public string NoiseChannel { get; private set; } = default!;

    /// <summary>
    ///     The index of ranges in which to cut debris generation.
    /// </summary>
    [DataField("ranges", required: true)]
    public List<Vector2> Ranges { get; private set; } = default!;
}

