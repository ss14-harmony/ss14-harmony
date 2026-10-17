using Content.Server._Harmony.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Server.Speech.Prototypes;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server._Harmony.Speech.EntitySystems;

public sealed partial class IrishAccentSystem : RelayAccentSystem<IrishAccentComponent>
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    private static readonly ProtoId<ReplacementAccentPrototype> AccentName = new("irish");

    // converts left word when typed into the right word. For example typing you becomes ye.
    public override string Accentuate(string message, Entity<IrishAccentComponent>? entity = null)
    {
        return _replacement.ApplyReplacements(message, AccentName);
    }
}
