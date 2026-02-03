using Content.Shared._Harmony.Bingle.EntitySystems;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Harmony.Bingle.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedBinglePitSystem))]
[AutoGenerateComponentState]
public sealed partial class BinglePitComponent : Component
{
    public const string PitContainerName = "pit";

    [ViewVariables]
    public Container Pit = default!;

    /// <summary>
    /// The amount of points this pit contains.
    /// This resets everytime the level increases.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Points;

    /// <summary>
    /// A specific point accumulator parallel to bingle points but only used when adding new bingles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BingleSpawnPoints;

    /// <summary>
    /// How many points are gained when an object is inserted into the pit.
    /// </summary>
    [DataField]
    public int PointsPerObject = 1;

    /// <summary>
    /// A bonus that is added to the given number of points whenever something *alive* is inserted into the pit.
    /// </summary>
    [DataField]
    public int AliveBonus = 4;

    /// <summary>
    /// The sound that plays when something falls into the pit.
    /// </summary>
    [DataField]
    public SoundSpecifier FallingSound = new SoundPathSpecifier("/Audio/Effects/falling.ogg");

    /// <summary>
    /// The current level that the bingle pit is at.
    /// This is also the level that the bingle pit will start at.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CurrentLevel;

    // This is probably not well-balanced at all, and will definitely need to be changed a lot.
    /// <summary>
    /// The list of levels that the bingle pit can reach.
    /// The lower in the list a level is, the later it happens.
    /// </summary>
    /// <remarks>
    /// Requiring less points to spawn a bingle than what is possible to gain is probably going to end up with something bad happening.
    /// </remarks>
    [DataField]
    public List<BinglePitLevel> Levels = new()
    {
        new(0, false, 2, 1f, 1), // level 1: bingles start and get a few bingles to start off with.
        new(10, true, 10, 2f, 5), // level 2: bingles slowly get bigger
        new(100, true, 50, 3f, 20, true), // level 3: max bingle capacity reached, much less bingles
    };

    /// <summary>
    /// The time between something touching the pit and being inserted.
    /// </summary>
    [DataField]
    public TimeSpan FallIntoPitTime = TimeSpan.FromSeconds(1.8f);

    /// <summary>
    /// The entity that the pit will spawn whenever wanting to add more bingles.
    /// </summary>
    [DataField]
    public EntProtoId GhostRoleToSpawn = "SpawnPointGhostBingle";

    /// <summary>
    /// The tile that the pit will replace the floor around it with.
    /// </summary>
    [DataField]
    public ProtoId<ContentTileDefinition> BingleTile = "FloorBingle";
}

/// <summary>
/// Defines the values for a single level of the bingle pit.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class BinglePitLevel
{
    /// <summary>
    /// The amount of points required before advancing to this level from the previous one.
    /// </summary>
    [DataField]
    public int PointsRequired;

    /// <summary>
    /// Should the pit be able to eat living beings at this level.
    /// </summary>
    [DataField]
    public bool CanEatLiving;

    /// <summary>
    /// The amount of points required in between each bingle.
    /// </summary>
    [DataField]
    public int PointsPerBingle;

    /// <summary>
    /// The size of the pit at that level.
    /// </summary>
    [DataField]
    public float Size;

    /// <summary>
    /// How far the bingle pit will place tiles.
    /// </summary>
    [DataField]
    public int TileRadius;

    /// <summary>
    /// Will the pit ignore whether people are weightless or not.
    /// </summary>
    [DataField]
    public bool IgnoreWeightless;

    public BinglePitLevel(int pointsRequired, bool canEatLiving, int pointsPerBingle, float size, int tileRadius, bool ignoreWeightless = false)
    {
        PointsRequired = pointsRequired;
        CanEatLiving = canEatLiving;
        PointsPerBingle = pointsPerBingle;
        Size = size;
        TileRadius = tileRadius;
        IgnoreWeightless = ignoreWeightless;
    }
}
