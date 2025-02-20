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
    /// A string containing a list of newline-separated words to be highlighted in the chat.
    /// </summary>
    public static readonly CVarDef<string> ChatHighlights =
        CVarDef.Create("chat.highlights", "", CVar.CLIENTONLY | CVar.ARCHIVE, "A list of newline-separated words to be highlighted in the chat.");

    /// <summary>
    /// An option to toggle the automatic filling of the highlights with the character's info, if available.
    /// </summary>
    public static readonly CVarDef<bool> ChatAutoFillHighlights =
        CVarDef.Create("chat.auto_fill_highlights", false, CVar.CLIENTONLY | CVar.ARCHIVE, "Toggles automatically filling the highlights with the character's information.");

    /// <summary>
    /// The color in which the highlights will be displayed.
    /// </summary>
    public static readonly CVarDef<string> ChatHighlightsColor =
        CVarDef.Create("chat.highlights_color", "#17FFC1FF", CVar.CLIENTONLY | CVar.ARCHIVE, "The color in which the highlights will be displayed.");
}
