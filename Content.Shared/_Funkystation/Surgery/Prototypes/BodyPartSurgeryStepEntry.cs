using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Prototypes;

/// <summary>
/// Defines a surgical step as a mechanical procedure plus optional flavor text and tool overrides.
/// Allows multiple steps with the same procedure but different display text per species.
/// </summary>
[DataDefinition]
public sealed partial class BodyPartSurgeryStepEntry
{
    /// <summary>
    /// The mechanical step (CreateIncision, ClampVessels, etc.) - defines tools, damage, prerequisites.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SurgeryProcedurePrototype> Procedure { get; private set; }

    /// <summary>
    /// Optional flavor text override. When null, use the procedure's default Name.
    /// </summary>
    [DataField]
    public LocId? Name { get; private set; }

    /// <summary>
    /// Override procedure's primary tool tag. When null, use procedure's PrimaryTool.Tag.
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype>? PrimaryTag { get; private set; }

    /// <summary>
    /// Override procedure's primary to damage type. When null, use procedure's PrimaryTool.
    /// </summary>
    [DataField]
    public ImprovisedDamageType? PrimaryDamageType { get; private set; }

    /// <summary>
    /// Override improvised tool tag. When null, use procedure's ImprovisedTools.
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype>? ImprovisedTag { get; private set; }

    /// <summary>
    /// Override improvised damage type. When null, use procedure's ImprovisedTools.
    /// </summary>
    [DataField]
    public ImprovisedDamageType? ImprovisedDamageType { get; private set; }
}
