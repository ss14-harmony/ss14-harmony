using Content.Shared.Damage;
using Content.Shared.Medical.Surgery;
using Robust.Shared.Audio;
using Robust.Shared.Localization;
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

    /// <summary>
    /// Longer explanatory text for this procedure. Locale lives in medical/surgery-procedures.ftl (surgery-procedure-desc-*).
    /// </summary>
    [DataField]
    public LocId? Description { get; private set; }

    /// <summary>
    /// Floating popup for the surgeon when this step's do-after starts. Pair with <see cref="EmoteStartOthers"/>.
    /// </summary>
    [DataField("emoteStartSelf")]
    public LocId? EmoteStartSelf { get; private set; }

    /// <summary>
    /// Floating popup for onlookers when this step's do-after starts.
    /// </summary>
    [DataField("emoteStartOthers")]
    public LocId? EmoteStartOthers { get; private set; }

    /// <summary>
    /// Floating popup for the surgeon when this step completes successfully. Pair with <see cref="EmoteCompleteOthers"/>.
    /// </summary>
    [DataField("emoteCompleteSelf")]
    public LocId? EmoteCompleteSelf { get; private set; }

    /// <summary>
    /// Floating popup for onlookers when this step completes successfully.
    /// </summary>
    [DataField("emoteCompleteOthers")]
    public LocId? EmoteCompleteOthers { get; private set; }

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
