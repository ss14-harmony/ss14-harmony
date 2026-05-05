using Content.Server.Medical.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.MedicalScanner;

namespace Content.Server.CartridgeLoader.Cartridges;

public sealed class MedTekCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoaderSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedTekCartridgeComponent, CartridgeAddedEvent>(OnCartridgeAdded);
        SubscribeLocalEvent<MedTekCartridgeComponent, CartridgeRemovedEvent>(OnCartridgeRemoved);
    }

    private void OnCartridgeAdded(Entity<MedTekCartridgeComponent> ent, ref CartridgeAddedEvent args)
    {
        //Funkystation: HealthAnalyzerComponent not a var, not sure why it was defined as one here
        EnsureComp<HealthAnalyzerComponent>(args.Loader);
        // Funkystation: SharedHealthAnalyzerComponent 
        EnsureComp<SharedHealthAnalyzerComponent>(args.Loader);
    }

    private void OnCartridgeRemoved(Entity<MedTekCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        // only remove when the program itself is removed
        if (!_cartridgeLoaderSystem.HasProgram<MedTekCartridgeComponent>(args.Loader))
        {
            RemComp<HealthAnalyzerComponent>(args.Loader);
            // Funkystation: SharedHealthAnalyzerComponent
            RemComp<SharedHealthAnalyzerComponent>(args.Loader);
        }
    }
}
