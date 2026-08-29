using Robust.Shared.Configuration;

namespace Content.Shared._Harmony.CCVars;

/// <summary>
/// Harmony-specific cvars.
/// </summary>
[CVarDefs]
public sealed class HCCVars
{
    /// <summary>
    /// Modifies suicide command to ghost without killing the entity.
    /// </summary>
    public static readonly CVarDef<bool> DisableSuicide =
        CVarDef.Create("ic.disable_suicide", false, CVar.SERVER);

    /// <summary>
    /// Allows server hosters to turn the queue on and off
    /// </summary>
    public static readonly CVarDef<bool> EnableQueue =
        CVarDef.Create("queue.enable", false, CVar.SERVER);

    /// <summary>
    /// The maximum number of people that can be in the queue at a time.
    /// If this is set to 0, an infinite number of people can connect to the queue.
    /// </summary>
    public static readonly CVarDef<int> MaxQueuePlayerCount =
        CVarDef.Create("queue.max_player_count", 0, CVar.SERVERONLY); // Client doesn't care about this CVar whatsoever

    /// <summary>
    /// If the content warning should be displayed.
    /// </summary>
    public static readonly CVarDef<bool> ContentWarningDisplay =
    CVarDef.Create("cw.display", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// If ignoring the content warning should kick you from the server.
    /// </summary>
    public static readonly CVarDef<bool> ContentWarningKickOnIgnore =
        CVarDef.Create("cw.kick", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// If the content warning popup was acknowledged.
    /// </summary>
    public static readonly CVarDef<bool> ContentWarningAcknowledged =
        CVarDef.Create("cw.acknowledged", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
