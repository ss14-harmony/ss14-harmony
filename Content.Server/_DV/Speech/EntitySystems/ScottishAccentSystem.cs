using Content.Server.DeltaV.Speech.Components;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server.DeltaV.Speech.EntitySystems;

public sealed partial class ScottishAccentSystem : RelayAccentSystem<ScottishAccentComponent>
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    // converts left word when typed into the right word. For example typing you becomes ye.
    public override string Accentuate(string message, Entity<ScottishAccentComponent>? ent = null)
    {
        var msg = message;

        msg = _replacement.ApplyReplacements(msg, "scottish");

        return msg;
    }
}
