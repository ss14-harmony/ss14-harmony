using System.Linq;
using Content.Server.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.Server.Cybernetics.Systems;

public sealed class CyberLiverSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private static readonly ProtoId<OrganCategoryPrototype> Liver = "Liver";
    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";

    private const float DamagePerMinutePer10PercentMissing = 1f;
    private const float HealingPerMinutePer10PercentAbove = 1f;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BodyComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out _, out var damageable))
        {
            var liver = _body.GetAllOrgans(uid).FirstOrDefault(o =>
                TryComp<OrganComponent>(o, out var oc) && oc.Category == Liver);
            if (liver == default || !TryComp<CyberOrganComponent>(liver, out var cyberLiver))
                continue;

            var effectiveness = cyberLiver.Effectiveness;
            float ratePerSecond;
            if (effectiveness < 1f)
            {
                var damagePerMinute = (1f - effectiveness) * 10f * DamagePerMinutePer10PercentMissing;
                ratePerSecond = damagePerMinute / 60f;
                var damageSpec = new DamageSpecifier();
                damageSpec.DamageDict[Poison] = FixedPoint2.New(ratePerSecond * frameTime);
                _damageable.TryChangeDamage((uid, damageable), damageSpec, interruptsDoAfters: false);
            }
            else if (effectiveness > 1f)
            {
                var healingPerMinute = (effectiveness - 1f) * 10f * HealingPerMinutePer10PercentAbove;
                ratePerSecond = healingPerMinute / 60f;
                var healAmount = FixedPoint2.New(ratePerSecond * frameTime);
                var healSpec = new DamageSpecifier();
                healSpec.DamageDict[Poison] = -healAmount;
                _damageable.TryChangeDamage((uid, damageable), healSpec, interruptsDoAfters: false);
            }
        }
    }
}
