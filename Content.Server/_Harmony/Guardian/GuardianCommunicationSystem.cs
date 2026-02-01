using Content.Server.Administration.Logs;
using Content.Server.Body.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Guardian;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Guardian;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Harmony.Guardian;

public sealed class GuardianCommunicationSystem : EntitySystem
{

    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly DamageableSystem _damageSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actionSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GuardianComponent, AccentGetEvent>(OnGuardianSpeakAcceent);
        SubscribeLocalEvent<GuardianHostComponent, AccentGetEvent>(OnHostSpeakAccent);
        SubscribeLocalEvent<GuardianHostComponent, GuardianCommunicationActionEvent>(OnHostCommunicationAction);
    }

    private void OnGuardianSpeakAcceent(EntityUid uid, GuardianComponent component, AccentGetEvent args)
    {
        if (component.GuardianLoose)
            return;

        if (component.Host == null)
            return;

        if (!TryComp<ActorComponent>(component.Host, out var actor) || !TryComp<MetaDataComponent>(component.Host, out var hostmeta))
            return;

        if (!TryComp<ActorComponent>(uid, out var actorguardian) || !TryComp<MetaDataComponent>(uid, out var guardianmeta))
            return;

        var message = Loc.GetString("guardian-speech", ("message", args.Message), ("name", guardianmeta.EntityName));
        var messageSelf = Loc.GetString("guardian-speech-self", ("message", args.Message), ("name", hostmeta.EntityName));
        var ghostmessage = Loc.GetString("guardian-speech-ghost", ("message", args.Message), ("hostname", hostmeta.EntityName), ("guardianname", guardianmeta.EntityName));

        _chatManager.ChatMessageToOne(ChatChannel.Local, message, message, default, false , actor.PlayerSession.Channel); // Message to the host
        _chatManager.ChatMessageToOne(ChatChannel.Local, messageSelf, messageSelf, default, false , actorguardian.PlayerSession.Channel); // message to the guardian

        // Ghost message logic
        var ghosts = new List<INetChannel>();
        var query = EntityQueryEnumerator<GhostComponent, ActorComponent>();
        while (query.MoveNext(out _, out _, out var actorghosts))
        {
            ghosts.Add(actorghosts.PlayerSession.Channel);
        }

        _chatManager.ChatMessageToMany(ChatChannel.Server, ghostmessage, ghostmessage, default, false, true, ghosts,Color.Orange); // Message to dead chat


        args.Message = "";
    }

    private void OnHostSpeakAccent(EntityUid uid, GuardianHostComponent component, AccentGetEvent args)
    {
        if (!component.SubtleCommunicationOn)
            return;

        if (component.HostedGuardian == null)
            return;

        if (!TryComp<ActorComponent>(component.HostedGuardian, out var actor) || !TryComp<MetaDataComponent>(component.HostedGuardian, out var guardianmeta))
            return;

        if (!TryComp<ActorComponent>(uid, out var actorhost) || !TryComp<MetaDataComponent>(uid, out var hostmeta))
            return;

        var message = Loc.GetString("host-speech", ("message", args.Message), ("name", guardianmeta.EntityName));
        var messageSelf = Loc.GetString("host-speech-self", ("message", args.Message), ("name", hostmeta.EntityName));
        var ghostmessage = Loc.GetString("host-speech-ghost", ("message", args.Message), ("hostname", hostmeta.EntityName), ("guardianname", guardianmeta.EntityName));

        _chatManager.ChatMessageToOne(ChatChannel.Server, message, message, default, false , actor!.PlayerSession.Channel); // Message to the guardian
        _chatManager.ChatMessageToOne(ChatChannel.Server, messageSelf, messageSelf, default, false , actorhost!.PlayerSession.Channel); // Message to the host

        // Ghost message logic
        var ghosts = new List<INetChannel>();
        var query = EntityQueryEnumerator<GhostComponent, ActorComponent>();
        while (query.MoveNext(out _, out _, out var actorghosts))
        {
            ghosts.Add(actorghosts.PlayerSession.Channel);
        }

        _chatManager.ChatMessageToMany(ChatChannel.Server, ghostmessage, ghostmessage, default, false, true, ghosts,Color.Orange); // Message to dead chat



        args.Message = "";
    }

    private void OnHostCommunicationAction(EntityUid uid, GuardianHostComponent component, GuardianCommunicationActionEvent args)
    {
        if (args.Handled)
            return;

        if (!component.SubtleCommunicationOn)
        {
            component.SubtleCommunicationOn = true;
            _popupSystem.PopupEntity(Loc.GetString("guardian-communication-enabled"), uid, args.Performer, PopupType.Medium);
        }
        else
        {
            component.SubtleCommunicationOn = false;
            _popupSystem.PopupEntity(Loc.GetString("guardian-communication-disabled"), uid, args.Performer, PopupType.Medium);
        }

        args.Handled = true;
    }
}
