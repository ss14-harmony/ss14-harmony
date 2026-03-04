using Content.Shared.DoAfter;
using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Surgery.Events;

[Serializable, NetSerializable]
public sealed partial class SurgeryDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public NetEntity BodyPart { get; private set; }

    [DataField(required: true)]
    public ProtoId<SurgeryProcedurePrototype> ProcedureId { get; private set; }

    [DataField]
    public NetEntity? Organ { get; private set; }

    [DataField]
    public bool IsImprovised { get; private set; }

    /// <summary>
    /// The tool entity used for the surgery (for playing tool-appropriate hit sounds).
    /// </summary>
    [DataField]
    public NetEntity? Tool { get; private set; }

    private SurgeryDoAfterEvent()
    {
    }

    public SurgeryDoAfterEvent(NetEntity bodyPart, ProtoId<SurgeryProcedurePrototype> procedureId, NetEntity? organ = null, bool isImprovised = false, NetEntity? tool = null)
    {
        BodyPart = bodyPart;
        ProcedureId = procedureId;
        Organ = organ;
        IsImprovised = isImprovised;
        Tool = tool;
    }

    public override DoAfterEvent Clone() => new SurgeryDoAfterEvent(BodyPart, ProcedureId, Organ, IsImprovised, Tool);
}
