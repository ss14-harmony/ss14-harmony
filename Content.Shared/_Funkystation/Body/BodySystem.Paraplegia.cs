using Content.Shared.Medical.Surgery.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

public sealed partial class BodySystem
{
    private static readonly ProtoId<OrganCategoryPrototype> ParaplegiaFootLeftCategory = "FootLeft";
    private static readonly ProtoId<OrganCategoryPrototype> ParaplegiaFootRightCategory = "FootRight";

    /// <summary>
    /// Stamp trait paraplegia onto every foot organ currently implanted in this body (traits and cloning).
    /// </summary>
    public void ApplyTraitParaplegiaToImplantedFeet(EntityUid body)
    {
        foreach (var organUid in GetAllOrgans(body))
        {
            if (!TryComp<OrganComponent>(organUid, out var organ))
                continue;

            if (organ.Category != ParaplegiaFootLeftCategory && organ.Category != ParaplegiaFootRightCategory)
                continue;

            var traitComp = EnsureComp<FootTraitParaplegicComponent>(organUid);
            Dirty(organUid, traitComp);
        }
    }

    /// <summary>
    /// Strip foot trait paraplegia from implanted feet (e.g. changeling / admin removal of the trait).
    /// </summary>
    public void RemoveTraitParaplegiaFromImplantedFeet(EntityUid body)
    {
        foreach (var organUid in GetAllOrgans(body))
        {
            if (!TryComp<OrganComponent>(organUid, out var organ))
                continue;

            if (organ.Category != ParaplegiaFootLeftCategory && organ.Category != ParaplegiaFootRightCategory)
                continue;

            RemComp<FootTraitParaplegicComponent>(organUid);
        }
    }
}
