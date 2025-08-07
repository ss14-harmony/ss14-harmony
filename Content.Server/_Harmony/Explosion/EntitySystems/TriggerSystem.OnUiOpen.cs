using Content.Server.Explosion.Components;

namespace Content.Server.Explosion.EntitySystems;

public sealed partial class TriggerSystem
{
    private void InitializeOnUiOpen()
    {
        SubscribeLocalEvent<TriggerOnUiOpenComponent, BoundUIOpenedEvent>(OnStorageOpen);
    }

    private void OnStorageOpen(Entity<TriggerOnUiOpenComponent> ent, ref BoundUIOpenedEvent args)
    {
        Trigger(ent.Owner, args.Actor);
    }
}
