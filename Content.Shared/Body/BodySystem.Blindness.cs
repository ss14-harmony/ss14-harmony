using Content.Shared.Body.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

public sealed partial class BodySystem
{
    private static readonly ProtoId<OrganCategoryPrototype> BlindnessOrganEyesCategory = "Eyes";

    /// <summary>
    /// Copy trait blindness severity onto every Eyes organ currently implanted in this body (used by traits and cloning).
    /// </summary>
    public void ApplyOrganTraitBlindnessToImplantedEyes(EntityUid body, int blindness)
    {
        foreach (var organUid in GetAllOrgans(body))
        {
            if (!TryComp<OrganComponent>(organUid, out var organ))
                continue;

            if (organ.Category != BlindnessOrganEyesCategory)
                continue;

            var traitComp = EnsureComp<OrganTraitBlindnessComponent>(organUid);
            traitComp.Blindness = blindness;
            Dirty(organUid, traitComp);
        }
    }

    /// <summary>
    /// Strip organ trait blindness from implanted eyes (when permanent blindness is removed from the mob, e.g. changeling).
    /// </summary>
    public void RemoveOrganTraitBlindnessFromImplantedEyes(EntityUid body)
    {
        foreach (var organUid in GetAllOrgans(body))
        {
            if (!TryComp<OrganComponent>(organUid, out var organ))
                continue;

            if (organ.Category != BlindnessOrganEyesCategory)
                continue;

            RemComp<OrganTraitBlindnessComponent>(organUid);
        }

        RecalculateBlindnessFromOrgans(body);
    }

    /// <summary>
    /// Update <see cref="BlindableComponent"/> trait floor from implanted eye organs.
    /// </summary>
    public void RecalculateBlindnessFromOrgans(EntityUid body)
    {
        if (TerminatingOrDeleted(body))
            return;

        if (!TryComp<BlindableComponent>(body, out var blindable))
            return;

        var eyeOrganCount = 0;
        var bestTraitBlindness = int.MaxValue;

        foreach (var organUid in GetAllOrgans(body))
        {
            if (!TryComp<OrganComponent>(organUid, out var organ))
                continue;

            if (organ.Category != BlindnessOrganEyesCategory)
                continue;

            eyeOrganCount++;

            if (TryComp<OrganTraitBlindnessComponent>(organUid, out var traitBlind))
                bestTraitBlindness = Math.Min(bestTraitBlindness, traitBlind.Blindness);
        }

        var blinding = EntityManager.System<BlindableSystem>();

        // No implanted eyes or no trait blindness on implanted eyes: clear the trait eye-damage floor.
        if (eyeOrganCount == 0 || bestTraitBlindness == int.MaxValue)
        {
            blinding.SetMinDamage((body, blindable), 0);
            // Lowering MinDamage alone does not reduce EyeDamage; heal accumulated damage only when implanted eyes have no trait blindness.
            if (eyeOrganCount > 0 && bestTraitBlindness == int.MaxValue && blindable.EyeDamage > 0)
                blinding.AdjustEyeDamage((body, blindable), -blindable.EyeDamage);
        }
        else if (bestTraitBlindness != 0)
        {
            blinding.SetMinDamage((body, blindable), bestTraitBlindness);
        }
        else
        {
            var maxMagnitudeInt = (int)BlurryVisionComponent.MaxMagnitude;
            blinding.SetMinDamage((body, blindable), maxMagnitudeInt);
        }

        blinding.UpdateIsBlind((body, blindable));
    }
}
