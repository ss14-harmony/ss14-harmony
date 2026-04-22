using Content.Server.EUI;
using Content.Server.Mind; // Funky - Cybermed
using Content.Shared.Eui;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Ghost;

public sealed class ReturnToBodyEui : BaseEui
{
    private readonly MindSystem _mindSystem; // Funky - Cybermed
    private readonly ISharedPlayerManager _player;
    private readonly NetUserId? _userId;

    public ReturnToBodyEui(MindComponent mind, MindSystem mindSystem, ISharedPlayerManager player) // Funky - Cybermed
    {
        _mindSystem = mindSystem;
        _player = player;
        _userId = mind.UserId;
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not ReturnToBodyMessage choice ||
            !choice.Accepted)
        {
            Close();
            return;
        }

        if (_userId is { } userId && _player.TryGetSessionById(userId, out var session)) // Funky - Cybermed
            _mindSystem.ReturnToBody(session); // Funky - Cybermed

        Close();
    }
}
