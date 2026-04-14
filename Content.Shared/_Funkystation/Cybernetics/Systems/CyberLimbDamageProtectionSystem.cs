using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cybernetics.Systems;

/// <summary>
/// Manages DamageProtectionBuffComponent on bodies with military cyber limbs.
/// Called from CyberLimbStatsSystem (which subscribes to attach/detach events) to avoid duplicate subscriptions.
/// </summary>
public sealed class CyberLimbDamageProtectionSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private const string MilitaryCyberlimbModifierId = "MilitaryCyberlimb5Percent";

    /// <summary>
    /// Call when a military cyber limb is attached. Adds damage protection if needed.
    /// </summary>
    public void OnMilitaryLimbAttached(EntityUid body)
    {
        EnsureMilitaryDamageProtection(body);
    }

    /// <summary>
    /// Call when a military cyber limb is detached. Recalculates and removes protection if no military limbs remain.
    /// </summary>
    public void OnMilitaryLimbDetached(EntityUid body)
    {
        RecalcMilitaryDamageProtection(body);
    }

    private void EnsureMilitaryDamageProtection(EntityUid body)
    {
        if (!_prototypeManager.TryIndex<DamageModifierSetPrototype>(MilitaryCyberlimbModifierId, out var modifierSet))
            return;

        var comp = EnsureComp<DamageProtectionBuffComponent>(body);
        if (!comp.Modifiers.ContainsKey("MilitaryCyberlimb"))
        {
            comp.Modifiers["MilitaryCyberlimb"] = modifierSet;
            Dirty(body, comp);
        }
    }

    private void RecalcMilitaryDamageProtection(EntityUid body)
    {
        var hasMilitaryLimb = _body.GetAllOrgans(body).Any(o => HasComp<MilitaryCyberLimbComponent>(o));

        if (!hasMilitaryLimb && TryComp<DamageProtectionBuffComponent>(body, out var comp))
        {
            comp.Modifiers.Remove("MilitaryCyberlimb");
            if (comp.Modifiers.Count == 0)
                RemComp<DamageProtectionBuffComponent>(body);
            else
                Dirty(body, comp);
        }
    }
}
