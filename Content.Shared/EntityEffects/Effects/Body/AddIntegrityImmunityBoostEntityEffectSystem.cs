using Content.Shared.Body;
using Content.Shared.Medical.Integrity.Components;
using Robust.Shared.Timing;

namespace Content.Shared.EntityEffects.Effects.Body;

/// <summary>
/// Adds or refreshes an integrity immunity boost on the organ that metabolizes Immunosuppressant.
/// </summary>
public sealed partial class AddIntegrityImmunityBoostEntityEffectSystem : EntityEffectSystem<OrganComponent, AddIntegrityImmunityBoost>
{
    [Dependency] private readonly IGameTiming _timing = default!;

    protected override void Effect(Entity<OrganComponent> entity, ref EntityEffectEvent<AddIntegrityImmunityBoost> args)
    {
        var amount = (int)MathF.Floor(args.Effect.Amount * args.Scale);
        if (amount <= 0)
            return;

        var comp = EnsureComp<IntegrityImmunityBoostComponent>(entity);
        comp.Amount = amount;
        comp.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(args.Effect.DurationSeconds);
        Dirty(entity, comp);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class AddIntegrityImmunityBoost : EntityEffectBase<AddIntegrityImmunityBoost>
{
    [DataField]
    public int Amount { get; private set; } = 1;

    [DataField]
    public float DurationSeconds { get; private set; } = 60f;
}
