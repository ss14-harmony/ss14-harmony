using Content.Server._Misfit.Announcements; // Misfit - Ion Storm Notifier
using Content.Server.Silicons.Laws;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Station.Components;
// Start CD - Synth Trait
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Robust.Shared.Player;
using Robust.Shared.Random;
// End CD - Synth Trait

namespace Content.Server.StationEvents.Events;

public sealed class IonStormRule : StationEventSystem<IonStormRuleComponent>
{
    [Dependency] private readonly IonStormSystem _ionStorm = default!;
    [Dependency] private readonly IChatManager _chatManager = default!; // CD - Synth Trait

    protected override void Started(EntityUid uid, IonStormRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (!TryGetRandomStation(out var chosenStation))
            return;

        // Begin Misfit - Moved CD's synth notifier to its own function
        var notifierQuery = EntityQueryEnumerator<IonStormNotifierComponent>();
        while (notifierQuery.MoveNext(out var ent, out var notifierComponent))
        {
            NotifyIonStorm(ent, notifierComponent.AlertChance, notifierComponent.Loc);
        }
        // End Misfit

        var query = EntityQueryEnumerator<SiliconLawBoundComponent, TransformComponent, IonStormTargetComponent>();
        while (query.MoveNext(out var ent, out var lawBound, out var xform, out var target))
        {
            // only affect law holders on the station
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != chosenStation)
                continue;

            _ionStorm.IonStormTarget((ent, lawBound, target));
        }
    }

    // Misfit - Move CD's ion storm notification to its own function
    private void NotifyIonStorm(EntityUid ent, float alertChance, string loc)
    {
        if (!Random.Shared.Prob(alertChance))
            return;

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var msg = Loc.GetString(loc);
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", msg));
        _chatManager.ChatMessageToOne(ChatChannel.Server, msg, wrappedMessage, default, false, actor.PlayerSession.Channel, colorOverride: Color.Yellow);
    }
}
