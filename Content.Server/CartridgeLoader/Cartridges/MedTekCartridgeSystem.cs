using Content.Server.Medical.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.MedicalScanner; // Funky - CyberMed

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
        //Funky - CybermMed: Start
        // HealthAnalyzerComponent not a var, not sure why it was defined as one here
        EnsureComp<HealthAnalyzerComponent>(args.Loader);
        EnsureComp<SharedHealthAnalyzerComponent>(args.Loader);
        // Funky - CyberMed: End
    }

    private void OnCartridgeRemoved(Entity<MedTekCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        // only remove when the program itself is removed
        if (!_cartridgeLoaderSystem.HasProgram<MedTekCartridgeComponent>(args.Loader))
        {
            RemComp<HealthAnalyzerComponent>(args.Loader);
            // Funky - CyberMed: SharedHealthAnalyzerComponent
            RemComp<SharedHealthAnalyzerComponent>(args.Loader);
        }
    }
}
