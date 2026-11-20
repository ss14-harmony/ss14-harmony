using Content.Shared._CD.Traits;

namespace Content.Shared.Humanoid;

public sealed partial class HumanoidProfileSystem
{
    public string GetSyntheticRepresentation(EntityUid uid, string speciesText)
    {
        return HasComp<SynthComponent>(uid)
            ? Loc.GetString("synthetic-component-examine", ("species", speciesText))
            : speciesText;
    }
}
