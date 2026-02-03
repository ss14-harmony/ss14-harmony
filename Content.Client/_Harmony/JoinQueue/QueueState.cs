using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Client._Harmony.JoinQueue;

public sealed class QueueState : State
{
    [Dependency] private readonly IClientJoinQueueManager _joinQueueManager = default!;
    [Dependency] private readonly IClientNetManager _netManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    protected override Type? LinkedScreenType { get; } = typeof(QueueGui);
    public QueueGui? Queue;

    private static readonly SoundSpecifier JoinSoundPath = new SoundPathSpecifier("/Audio/Effects/newplayerping.ogg");

    protected override void Startup()
    {
        if (_userInterfaceManager.ActiveScreen == null)
        {
            return;
        }

        Queue = (QueueGui)_userInterfaceManager.ActiveScreen;
        Queue.UpdateBranding(
            _resourceCache.GetResource<TextureResource>("/Textures/Logo/logo.png"),
            String.IsNullOrEmpty(_cfg.GetCVar(CCVars.ServerLobbyName)) ? _cfg.GetCVar(CVars.GameHostName) : _cfg.GetCVar(CCVars.ServerLobbyName));

        Queue.QuitButton.OnPressed += OnQuitButtonPressed;

        _joinQueueManager.QueueStateUpdated += OnQueueStateUpdated;
        OnQueueStateUpdated(); // Update the current state, even if it might be incorrect.
    }

    protected override void Shutdown()
    {
        _joinQueueManager.QueueStateUpdated -= OnQueueStateUpdated;

        if (_entityManager.TrySystem<AudioSystem>(out var audio))
            audio.PlayGlobal(JoinSoundPath, Filter.Local(), false);
    }

    private void OnQuitButtonPressed(BaseButton.ButtonEventArgs args)
    {
        _netManager.ClientDisconnect(_loc.GetString("queue-disconnect-reason"));
    }

    private void OnQueueStateUpdated()
    {
        Queue?.UpdateInfo(_joinQueueManager.PlayerInQueueCount, _joinQueueManager.CurrentPosition);
    }
}
