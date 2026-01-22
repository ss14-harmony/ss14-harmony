using Content.Shared.Body.Systems; // Misfit - Move synthetic trait to shared
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._CD.Traits; // Misfit - Move synthetic trait to shared

public sealed class SynthSystem : EntitySystem
{
    private static readonly ProtoId<TypingIndicatorPrototype> RobotTypingIndicator = "robot"; // Misfit - Type safety
    private static readonly ProtoId<ReagentPrototype> SynthBlood = "SynthBlood"; // Misfit - Type safety

    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!; // Misfit - Move synthetic trait to shared
    [Dependency] private readonly SharedTypingIndicatorSystem _typingIndicator = default!; // Misfit - Partial typing indicator change

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SynthComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, SynthComponent component, ComponentStartup args)
    {
        _typingIndicator.SetIndicatorPrototype(uid, RobotTypingIndicator); // Misfit - Type safety and partial typing indicator change

        // Give them synth blood. Ion storm notif is handled in that system
        _bloodstream.ChangeBloodReagent(uid, SynthBlood); // Misfit - Type safety
    }
}
