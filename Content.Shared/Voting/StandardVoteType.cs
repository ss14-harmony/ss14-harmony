namespace Content.Shared.Voting;

/// <summary>
/// Standard vote types that players can initiate themselves from the escape menu.
/// </summary>
public enum StandardVoteType : byte
{
    /// <summary>
    /// Vote to restart the round.
    /// </summary>
    Restart,

    /// <summary>
    /// Vote to change the game preset for next round.
    /// </summary>
    Preset,

    /// <summary>
    /// Vote to change the map for the next round.
    /// </summary>
    Map,

    /// <summary>
    /// Vote to kick a player.
    /// </summary>
    Votekick,

    // Start of Harmony additions: Change Auto evac call to OOC

    /// <summary>
    /// Auto magical evac call.
    /// </summary>
    AutoEvacCall

    // End of Harmony additions
}

/// <summary>
/// Reasons available to initiate a votekick.
/// </summary>
public enum VotekickReasonType : byte
{
    Raiding,
    Cheating,
    Spam
}
