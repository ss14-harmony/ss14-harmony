using Content.Server._Harmony.Spawners.Components;

namespace Content.Server._Harmony.Spawners.EntitySystems;

public sealed class SpawnOnSpawnSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnOnSpawnComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SpawnOnSpawnComponent> entity, ref MapInitEvent args)
    {
        foreach (var prototype in entity.Comp.Prototypes)
        {
            Spawn(prototype, Transform(entity).Coordinates);
        }
    }
}
