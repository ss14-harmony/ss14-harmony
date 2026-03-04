using Content.Shared.Damage;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Prototypes;

/// <summary>
/// Specifies the primary (proper) tool for a surgery procedure.
/// </summary>
[DataDefinition]
public sealed partial class PrimaryToolSpec
{
    /// <summary>
    /// Tag required on the held item (e.g. CuttingTool, Wirecutter).
    /// </summary>
    [DataField(required: true)]
    public ProtoId<TagPrototype> Tag { get; private set; }

    /// <summary>
    /// DoAfter duration in seconds when using the primary tool.
    /// </summary>
    [DataField]
    public float DoAfterDelay { get; private set; } = 2f;

    /// <summary>
    /// Damage applied when performing opening steps.
    /// </summary>
    [DataField]
    public DamageSpecifier? Damage { get; private set; }

    /// <summary>
    /// Healing on closing steps - DamageSpecifier with negative values.
    /// </summary>
    [DataField]
    public DamageSpecifier? HealAmount { get; private set; }

    /// <summary>
    /// Sound played when the step is performed.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound { get; private set; }
}
