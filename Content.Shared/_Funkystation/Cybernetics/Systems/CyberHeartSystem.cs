using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Robust.Shared.Prototypes;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Damage;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared.Cybernetics.Systems;

public sealed class CyberHeartSystem : EntitySystem
{
    private static readonly ProtoId<OrganCategoryPrototype> Heart = "Heart";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, GetOrganMetabolismScaleModifierEvent>(OnGetOrganMetabolismScaleModifier);
    }

    private void OnGetOrganMetabolismScaleModifier(Entity<BodyComponent> ent, ref GetOrganMetabolismScaleModifierEvent args)
    {
        if (!TryComp<OrganComponent>(args.Organ, out var organComp) || organComp.Category != Heart)
            return;

        if (!TryComp<CyberOrganComponent>(args.Organ, out var cyberOrgan))
            return;

        var effectiveness = cyberOrgan.Effectiveness;
        var modifier = 1f;

        switch (args.Effect)
        {
            case HealthChange healthChange:
                var total = healthChange.Damage.GetTotal();
                if (total > FixedPoint2.Zero)
                    modifier = effectiveness < 1f ? 1f / effectiveness : 1f;
                else if (total < FixedPoint2.Zero)
                    modifier = effectiveness;
                break;
            case DistributedHealthChange distHealthChange:
                var distTotal = FixedPoint2.Zero;
                foreach (var amount in distHealthChange.Damage.Values)
                    distTotal += amount;
                if (distTotal > FixedPoint2.Zero)
                    modifier = effectiveness < 1f ? 1f / effectiveness : 1f;
                else if (distTotal < FixedPoint2.Zero)
                    modifier = effectiveness;
                break;
            case EvenHealthChange evenHealthChange:
                var evenTotal = FixedPoint2.Zero;
                foreach (var amount in evenHealthChange.Damage.Values)
                    evenTotal += amount;
                if (evenTotal > FixedPoint2.Zero)
                    modifier = effectiveness < 1f ? 1f / effectiveness : 1f;
                else if (evenTotal < FixedPoint2.Zero)
                    modifier = effectiveness;
                break;
            default:
                return;
        }

        args.Scale *= modifier;
    }
}
