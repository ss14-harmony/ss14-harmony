using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Xenograft;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Integrity.Components;

/// <summary>
/// Integrity penalty stored on a body part (organ, limb, implant). Applied when the part is installed or receives a penalty.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(IntegrityPenaltyAggregatorSystem), typeof(OrganXenograftSystem))]
public sealed partial class IntegrityPenaltyComponent : Component
{
    /// <summary>
    /// The integrity penalty amount for this part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Penalty;

    /// <summary>
    /// Xenograft mismatch penalty (distinct from surgery-applied <see cref="Penalty"/>).
    /// </summary>
    [DataField, AutoNetworkedField]
    public int XenograftPenalty;
}
