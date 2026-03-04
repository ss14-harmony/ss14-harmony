using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Prototypes;

/// <summary>
/// Specifies an improvised tool that can be used for a surgery procedure.
/// Matches by tag or damage type; applies delay multiplier when used.
/// </summary>
[DataDefinition]
public sealed partial class ImprovisedToolSpec
{
    /// <summary>
    /// Match items with this tag. Takes precedence over damageType.
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype>? Tag { get; private set; }

    /// <summary>
    /// Match MeleeWeapon by damage type when tag is not specified.
    /// </summary>
    [DataField]
    public ImprovisedDamageType? DamageType { get; private set; }

    /// <summary>
    /// Multiplier for DoAfterDelay when using this improvised tool (e.g. 1.5 = 50% longer).
    /// </summary>
    [DataField]
    public float DelayMultiplier { get; private set; } = 1.5f;

    /// <summary>
    /// When DamageType is Blunt, scale delay by baseline/damage (e.g. 20 blunt = 1x speed).
    /// Null means use DelayMultiplier instead.
    /// </summary>
    [DataField]
    public float? BluntSpeedBaseline { get; private set; }
}

/// <summary>
/// Damage types for improvised tool matching.
/// </summary>
public enum ImprovisedDamageType
{
    Slash,
    Heat,
    Blunt
}
