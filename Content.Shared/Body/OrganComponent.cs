using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BodySystem), typeof(Content.Shared.Medical.Integrity.IntegrityUsageSystem))]
public sealed partial class OrganComponent : Component
{
    /// <summary>
    /// The body entity containing this organ, if any
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    /// <summary>
    /// What kind of organ is this, if any
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype>? Category;

    /// <summary>
    /// Integrity cost this organ consumes when installed. Natural organs use 0; biosynthetic/implants typically use 1.
    /// </summary>
    [DataField]
    public int IntegrityCost { get; set; }
}
