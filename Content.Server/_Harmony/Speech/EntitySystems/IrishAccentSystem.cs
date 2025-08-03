using Content.Server._Harmony.Speech.Components;
using Content.Server.Speech;
using Content.Server.Speech.EntitySystems;

namespace Content.Server._Harmony.Speech.EntitySystems;

public sealed class IrishAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;
    
    private const string accentname = "irish";
    
    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<IrishAccentComponent, AccentGetEvent>(OnAccentGet);
    }
    
    // converts left word when typed into the right word. For example typing you becomes ye.
    public string Accentuate(string message)
    {
        return _replacement.ApplyReplacements(message, accentname);
    }
    
    private void OnAccentGet(Entity<IrishAccentComponent> entity, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
