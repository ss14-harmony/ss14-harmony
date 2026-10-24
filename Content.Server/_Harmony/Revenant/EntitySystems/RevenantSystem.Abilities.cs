using Robust.Shared.Player;
using Content.Shared._Harmony.Revenant;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Revenant.Components;
using Robust.Shared.Utility;

namespace Content.Server.Revenant.EntitySystems;

public sealed partial class RevenantSystem
{
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;

    private void InitializeHarmonyAbilities()
    {
        SubscribeLocalEvent<RevenantComponent, RevenantWhisperActionEvent>(OnWhisperAction);
    }

    private void OnWhisperAction(EntityUid uid, RevenantComponent component, EntityTargetActionEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target == args.Performer)
            return;

        if (!TryComp(args.Performer, out ActorComponent? actorS) || !TryComp(args.Target, out ActorComponent? actorT))
            return;

        _quickDialog.OpenDialog(actorS.PlayerSession, Loc.GetString("revenant-ghostly-whisper-title", ("target", Identity.Entity(args.Target, EntityManager))), Loc.GetString("prayer-popup-notify-pray-ui-message"), (string message) =>
        {
            // Prayer popup still works fine for this
            // Make sure the player's entity still exist and they are very much still a revenant
            if (actorS?.PlayerSession != null && HasComp<RevenantComponent>(uid))
                Whisper(actorT.PlayerSession, actorS.PlayerSession, message);
        });

        args.Handled = true;
    }

    private void Whisper(ICommonSession target, ICommonSession sender , string message)
    {
        if (target.AttachedEntity == null || sender.AttachedEntity == null)
            return;

        var wrappedMessage = Loc.GetString("chat-manager-entity-whisper-wrap-message", ("entityName", Identity.Name(sender.AttachedEntity.Value, EntityManager)), ("message", FormattedMessage.EscapeText(message)));

        _popup.PopupEntity(Loc.GetString("revenant-ghostly-whisper-popup"), target.AttachedEntity.Value, target, PopupType.Medium);
        _chatManager.ChatMessageToOne(ChatChannel.Whisper, message, wrappedMessage, target.AttachedEntity.Value, false, target.Channel); // better to show a message to the player in question than make it text chat only
        _adminLogger.Add(LogType.AdminMessage, LogImpact.Low, $"{ToPrettyString(target.AttachedEntity.Value):player} received a ghostly whisper  from {sender?.Name ?? "unknown sender"}: {message}"); // Logging as Admin since it IS a Subtle Message
    }
}
