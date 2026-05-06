using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Funkystation.Body.Organs;

/// <summary>
/// Placed on an organ entity. While the organ is inserted into a body, the listed
/// components are added to the body. They are removed when the organ leaves the body.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrganBodyComponentsComponent : Component
{
    /// <summary>
    /// Components to add to the host body while this organ is implanted.
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry Components = new();
}
