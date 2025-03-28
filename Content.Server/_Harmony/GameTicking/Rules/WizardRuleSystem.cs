using Content.Server._Harmony.GameTicking.Rules.Components;
using Content.Shared._Harmony.Wizard;
using Content.Server.GameTicking.Rules;
using Content.Shared.Mobs;
using System.Linq;
using Content.Shared.Mobs.Components;
using Content.Server.RoundEnd;
using Robust.Shared.Timing;

namespace Content.Server._Harmony.GameTicking.Rules;

public sealed class WizardRuleSystem : GameRuleSystem<WizardRuleComponent>
{
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleRecalledEvent>(_ => OnShuttleRecall());
        SubscribeLocalEvent<WizardComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<WizardComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnComponentRemove(EntityUid uid, WizardComponent component, ComponentRemove args)
    {
        CheckRoundShouldEnd();
    }

    private void OnMobStateChanged(EntityUid uid, WizardComponent component, MobStateChangedEvent ev)
    {
        if (ev.NewMobState == MobState.Dead)
            CheckRoundShouldEnd();
    }

    // This is more or less copied from NukeopsRuleSystem. All I know is that it works.
    private void CheckRoundShouldEnd()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var wizard, out _))
        {
            CheckRoundShouldEnd(wizard);
        }
    }

    private void CheckRoundShouldEnd(WizardRuleComponent wizardRule)
    {
        // Only checks if the mob with the wizard component is alive somewhere. Doesn't check where.
        var wizard = EntityQuery<WizardComponent, MobStateComponent>(true);
        var wizardAlive = wizard
            .Any(wiz => wiz.Item2.CurrentState == MobState.Alive && wiz.Item1.Running);

        if (wizardAlive)
            return;

        _roundEndSystem.DoRoundEndBehavior(wizardRule.RoundEndBehavior,
            wizardRule.EvacShuttleTime,
            wizardRule.RoundEndTextSender,
            wizardRule.RoundEndTextShuttleCall,
            wizardRule.RoundEndTextAnnouncement);

        // Don't call multiple times
        wizardRule.RoundEndBehavior = RoundEndBehavior.Nothing;
        // Set the flag to schedule a sleeper event if the automatic shuttle is recalled to keep the round interesting post-wizard.
        wizardRule.AwaitingPossibleRecall = true;
    }

    private void OnShuttleRecall()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var wizardRule, out _))
        {
            if (wizardRule.AwaitingPossibleRecall == true)
            {
                // Schedules a sleeper event in 5 minutes.
                wizardRule.SleeperTime = _timing.CurTime + TimeSpan.FromMinutes(5);
                wizardRule.AwaitingPossibleRecall = false;
            }
        }
    }

    private void TryAddSleeper(WizardRuleComponent wizardRule)
    {
        // Sleeper agent events are only meant to trigger once per round.
        // We're going to keep this true for the forced sleeper event, and first check if one has occurred already.

        // Note that the event used here is a separate version parented from the normal event, but it will still check if the original
        // event has happened before as they are functionally identical. The wizard version may be configured later.
        // If this should stack on top of the normal sleeper event instead of being blocked by it, then the below check can be commented out.
        if (GameTicker.AllPreviousGameRules.Any(p => p.Item2 == "SleeperAgents"))
        {
            Log.Info("Tried to start a sleeper agent event, but one has already occurred");
            return;
        }

        GameTicker.AddGameRule("PostWizardSleeperAgents");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var wizardRule, out _))
        {
            if (wizardRule.SleeperTime != TimeSpan.Zero && _timing.CurTime > wizardRule.SleeperTime)
            {
                TryAddSleeper(wizardRule);
                wizardRule.SleeperTime = TimeSpan.Zero;
            }
        }
    }
}
