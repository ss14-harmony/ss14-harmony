using Content.Server._Harmony.StationEvents.Components;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Harmony.StationEvents.Events;

public sealed class GhostPingRule : StationEventSystem<GhostPingRuleComponent>
{
    [Dependency] private readonly IChatManager _chatManager = default!;

    protected override void Added(EntityUid uid, GhostPingRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        var ghosts = new List<INetChannel>();
        var query = EntityQueryEnumerator<GhostComponent, ActorComponent>();
        while (query.MoveNext(out _, out _, out var actorghosts))
        {
            ghosts.Add(actorghosts.PlayerSession.Channel);
        }

        var Annoucement = Loc.GetString(component.Announcement);
        var PingAudio = component.PingAudio;

        _chatManager.ChatMessageToMany(ChatChannel.Server,Annoucement, Annoucement, default, false, true, ghosts,Color.Gold, PingAudio); // Message to dead chat
    }
}
