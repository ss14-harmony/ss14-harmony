using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Defines the removal and insertion procedure lists for an organ.
/// Organs without this component fall back to OrganCategoryPrototype defaults or BodyPartSurgeryStepsPrototype.organSteps.
/// </summary>
[RegisterComponent]
public sealed partial class OrganSurgeryProceduresComponent : Component
{
    /// <summary>
    /// Ordered removal procedures. Completing the last one triggers organ removal.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<SurgeryProcedurePrototype>> RemovalProcedures { get; private set; } = new();

    /// <summary>
    /// Ordered insertion procedures to clear integrity penalties after organ insertion.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<SurgeryProcedurePrototype>> InsertionProcedures { get; private set; } = new();
}
