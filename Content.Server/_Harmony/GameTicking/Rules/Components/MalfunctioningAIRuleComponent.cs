namespace Content.Server._Harmony.GameTicking.Rules.Components;

/// <summary>
/// Stores data for <see cref="MalfunctioningAIRuleSystem"/>.
/// </summary>
[RegisterComponent, Access(typeof(MalfunctioningAIRuleSystem))]
public sealed partial class MalfunctioningAIRuleComponent : Component
{
    /// <summary>
    /// Whether or not the malfunctioning AI is "present"; if there is no AI then there will be no zeroth laws
    /// </summary>
    public bool Active = false;

    /// <summary>
    ///  Whether or not the Malf AI has successfully set off a Doomsday Device. If so, all of their objectives are automatically completed.
    /// </summary>
    public bool DoomsdayActivated = false;
}
