using Content.Shared.Body.Components; // Harmony
using Content.Shared.Body.Systems; // Misfit - Move synthetic trait to shared
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Chemistry.Components; // Harmony
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Forensics; // Harmony
using Content.Shared.Forensics.Components; // Harmony
using Robust.Shared.Prototypes;

namespace Content.Shared._CD.Traits; // Misfit - Move synthetic trait to shared

public sealed partial class SynthSystem : EntitySystem
{
    private static readonly ProtoId<TypingIndicatorPrototype> RobotTypingIndicator = "robot"; // Misfit - Type safety
    private static readonly ProtoId<ReagentPrototype> SynthBlood = "SynthBlood"; // Misfit - Type safety

    [Dependency] private SharedBloodstreamSystem _bloodstream = default!; // Misfit - Move synthetic trait to shared
    [Dependency] private SharedTypingIndicatorSystem _typingIndicator = default!; // Misfit - Partial typing indicator change

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SynthComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, SynthComponent component, ComponentStartup args)
    {
        _typingIndicator.SetIndicatorPrototype(uid, RobotTypingIndicator); // Misfit - Type safety and partial typing indicator change

        // Harmony Start - Update for new bloodstream system
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstreamComponent))
            return;

        var bloodstreamVolume = bloodstreamComponent.BloodReferenceSolution.Volume;

        var synthBloodSolution = new Solution(SynthBlood, bloodstreamVolume);
        // Harmony End

        // Give them synth blood. Ion storm notif is handled in that system
        _bloodstream.ChangeBloodReagents(uid, synthBloodSolution); // Misfit - Type safety // Harmony - Update to ChangeBloodReagents

        // Harmony Start - Regenerate DNA
        if (!TryComp<DnaComponent>(uid, out var dnaComp) || dnaComp.DNA == null)
            return;

        var ev = new GenerateDnaEvent { Owner = uid, DNA = dnaComp.DNA };
        RaiseLocalEvent(uid, ref ev);
        // Harmony End
    }
}
