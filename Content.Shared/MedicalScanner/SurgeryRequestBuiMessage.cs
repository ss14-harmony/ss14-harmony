using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

[Serializable, NetSerializable]
public sealed class SurgeryRequestBuiMessage : BoundUserInterfaceMessage
{
    public NetEntity Target;
    public NetEntity BodyPart;
    public ProtoId<SurgeryProcedurePrototype> ProcedureId;
    public SurgeryLayer Layer;
    public bool IsImprovised;
    public NetEntity? Organ;

    public SurgeryRequestBuiMessage(NetEntity target, NetEntity bodyPart, ProtoId<SurgeryProcedurePrototype> procedureId, SurgeryLayer layer, bool isImprovised, NetEntity? organ = null)
    {
        Target = target;
        BodyPart = bodyPart;
        ProcedureId = procedureId;
        Layer = layer;
        IsImprovised = isImprovised;
        Organ = organ;
    }
}
