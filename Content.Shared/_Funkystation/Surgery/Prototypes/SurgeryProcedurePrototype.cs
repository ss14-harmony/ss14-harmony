using Content.Shared.Damage;
using Content.Shared.Medical.Surgery;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Prototypes;

/// <summary>
/// Defines a surgical procedure with primary and improvised tool pairs.
/// Replaces SurgeryStepPrototype for data-driven procedure resolution.
/// </summary>
[Prototype]
public sealed partial class SurgeryProcedurePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public SurgeryLayer Layer { get; private set; }

    [DataField]
    public LocId? Name { get; private set; }

    [DataField]
    public int Penalty { get; private set; }

    /// <summary>
    /// When false, no tool is required (e.g. AttachLimb only needs the limb, DetachFoot needs nothing).
    /// </summary>
    [DataField]
    public bool RequiresTool { get; private set; } = true;

    [DataField]
    public PrimaryToolSpec PrimaryTool { get; private set; } = default!;

    [DataField]
    public List<ImprovisedToolSpec> ImprovisedTools { get; private set; } = new();

    /// <summary>
    /// Damage applied when performing opening steps. Overrides PrimaryTool.Damage when set.
    /// </summary>
    [DataField]
    public DamageSpecifier? Damage { get; private set; }

    /// <summary>
    /// Healing on closing steps. Overrides PrimaryTool.HealAmount when set.
    /// </summary>
    [DataField]
    public DamageSpecifier? HealAmount { get; private set; }

    [DataField]
    public SoundSpecifier? Sound { get; private set; }

    [DataField]
    public List<StepPrerequisite> Prerequisites { get; private set; } = new();

    /// <summary>
    /// For close steps: the open procedure this step undoes.
    /// </summary>
    [DataField]
    public ProtoId<SurgeryProcedurePrototype>? UndoesProcedure { get; private set; }

    /// <summary>
    /// When true, completing this procedure triggers organ removal (last step of removal flow).
    /// </summary>
    [DataField]
    public bool TriggersOrganRemoval { get; private set; }
}
