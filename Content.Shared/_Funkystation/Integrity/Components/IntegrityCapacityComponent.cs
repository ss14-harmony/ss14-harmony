using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Integrity.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(BioRejectionSystem))]
public sealed partial class IntegrityCapacityComponent : Component
{
    /// <summary>
    /// Maximum integrity capacity for this body. When usage + penalties exceed this, bio-rejection damage is applied.
    /// </summary>
    [DataField]
    public int MaxIntegrity { get; set; } = 6;
}
