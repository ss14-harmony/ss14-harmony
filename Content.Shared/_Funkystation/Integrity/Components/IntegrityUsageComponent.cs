using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Integrity.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(IntegrityUsageSystem))]
public sealed partial class IntegrityUsageComponent : Component
{
    /// <summary>
    /// Sum of IntegrityCost of all consuming organs in this body.
    /// </summary>
    [DataField]
    public int Usage { get; set; }
}
