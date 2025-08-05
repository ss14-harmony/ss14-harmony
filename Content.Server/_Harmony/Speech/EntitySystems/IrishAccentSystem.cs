using Content.Server._Harmony.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Server.Speech.Prototypes;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Server._Harmony.Speech.EntitySystems;

public sealed class IrishAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;
    
    private static readonly ProtoId<ReplacementAccentPrototype> AccentName = new("irish");
    
    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<IrishAccentComponent, AccentGetEvent>(OnAccentGet);
    }
    
    // converts left word when typed into the right word. For example typing you becomes ye.
    public string Accentuate(string message)
    {
        return _replacement.ApplyReplacements(message, AccentName);
    }
    
    private void OnAccentGet(Entity<IrishAccentComponent> entity, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
