using Content.Shared.Roles;

namespace Content.Server._Harmony.Roles;

/// <summary>
/// Added to mind role entities to tag that they are a blood bound.
/// </summary>
[RegisterComponent]
public sealed partial class BloodBoundRoleComponent : BaseMindRoleComponent
{
    [DataField]
    public EntityUid? Bound;
}
