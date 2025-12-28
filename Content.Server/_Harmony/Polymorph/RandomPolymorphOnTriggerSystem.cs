using Content.Server.Polymorph.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Harmony.Polymorph;

public sealed partial class RandomPolymorphOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private Queue<(EntityUid Uid, ProtoId<PolymorphPrototype> Polymorph)> _queuedPolymorphUpdates = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<Shared._Harmony.Polymorph.RandomPolymorphOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<Shared._Harmony.Polymorph.RandomPolymorphOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;

        if (target == null)
            return;

        var Polymorph = _random.Pick(ent.Comp.Polymorph!);

        _queuedPolymorphUpdates.Enqueue((target.Value, Polymorph));
        args.Handled = true;
    }

    public override void Update(float frametime)
    {
        while (_queuedPolymorphUpdates.TryDequeue(out var data))
        {
            if (TerminatingOrDeleted(data.Uid))
                continue;

            _polymorph.PolymorphEntity(data.Uid, data.Polymorph);
        }
    }
}
