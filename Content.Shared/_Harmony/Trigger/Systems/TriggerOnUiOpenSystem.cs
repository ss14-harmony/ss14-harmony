using Content.Shared._Harmony.Trigger.Components.Triggers;
using Content.Shared.Trigger.Systems;

namespace Content.Shared._Harmony.Trigger.Systems;

public sealed class TriggerOnUiOpenSystem : EntitySystem
{
    [Dependency] private readonly TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggerOnUiOpenComponent, BoundUIOpenedEvent>(OnUiOpen);
    }

    private void OnUiOpen(Entity<TriggerOnUiOpenComponent> entity, ref BoundUIOpenedEvent args)
    {
        _trigger.Trigger(entity, args.Actor, entity.Comp.KeyOut);
    }
}
