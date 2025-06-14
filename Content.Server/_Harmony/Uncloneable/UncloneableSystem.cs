using Content.Shared.Cloning.Events;
using Content.Server._Harmony.Uncloneable;

namespace Content.Server._Harmony.Uncloneable;

public sealed class UncloneableSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UncloneableComponent, CloningAttemptEvent>(OnCloningAttempt);
    }

    private void OnCloningAttempt(Entity<UncloneableComponent> ent, ref CloningAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
