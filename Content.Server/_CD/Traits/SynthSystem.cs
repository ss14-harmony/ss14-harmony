using Content.Server.Body.Systems;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._CD.Traits;

public sealed class SynthSystem : EntitySystem
{
    private static readonly ProtoId<TypingIndicatorPrototype> RobotTypingIndicator = "robot"; // Misfit - Type safety
    private static readonly ProtoId<ReagentPrototype> SynthBlood = "SynthBlood"; // Misfit - Type safety

    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SynthComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, SynthComponent component, ComponentStartup args)
    {
        if (TryComp<TypingIndicatorComponent>(uid, out var indicator))
        {
            indicator.TypingIndicatorPrototype = RobotTypingIndicator; // Misfit - Type safety
            Dirty(uid, indicator);
        }

        // Give them synth blood. Ion storm notif is handled in that system
        _bloodstream.ChangeBloodReagent(uid, SynthBlood); // Misfit - Type safety
    }
}
