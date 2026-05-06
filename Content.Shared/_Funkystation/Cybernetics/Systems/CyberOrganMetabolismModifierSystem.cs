using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Damage;
using Content.Shared.EntityEffects.Effects.Body;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Xenograft;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cybernetics.Systems;

/// <summary>
/// Scales metabolism entity effects based on cyber organ tier (heart, stomach, liver, lungs).
/// </summary>
public sealed class CyberOrganMetabolismModifierSystem : EntitySystem
{
    [Dependency] private readonly OrganXenograftSystem _organXenograft = default!;

    private static readonly ProtoId<OrganCategoryPrototype> Heart = "Heart";
    private static readonly ProtoId<OrganCategoryPrototype> Stomach = "Stomach";
    private static readonly ProtoId<OrganCategoryPrototype> Liver = "Liver";
    private static readonly ProtoId<OrganCategoryPrototype> Lungs = "Lungs";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, GetOrganMetabolismScaleModifierEvent>(OnGetOrganMetabolismScaleModifier);
    }

    private void OnGetOrganMetabolismScaleModifier(Entity<BodyComponent> ent, ref GetOrganMetabolismScaleModifierEvent args)
    {
        if (TryComp<OrganComponent>(args.Organ, out var organComp) && organComp.Category.HasValue
            && TryComp<CyberOrganComponent>(args.Organ, out var cyberOrgan))
        {
            ApplyCyberOrganMetabolismModifier(organComp.Category.Value, cyberOrgan.Effectiveness, ref args);
        }

        // Xenograft applies to any implanted organ with OrganXenograftComponent (including non-cyber).
        _organXenograft.ApplyForeignHostMetabolismScale(ent.Owner, ref args);
    }

    private void ApplyCyberOrganMetabolismModifier(
        ProtoId<OrganCategoryPrototype> category,
        float effectiveness,
        ref GetOrganMetabolismScaleModifierEvent args)
    {
        if (category == Lungs && args.Effect is ModifyLungGas)
        {
            args.Scale *= effectiveness;
            return;
        }

        if (category != Heart && category != Stomach && category != Liver)
            return;

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
