using Content.Server._Harmony.Spawners.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server._Harmony.Spawners.Components;

// I love this name
/// <summary>
/// When an entity with this component spawns, it will spawn all the entities in <see cref="Prototypes"/>.
/// </summary>
[RegisterComponent, Access(typeof(SpawnOnSpawnSystem))]
public sealed partial class SpawnOnSpawnComponent : Component
{
    /// <summary>
    /// The prototypes to spawn.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Prototypes = [];
}
