using Content.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

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
}