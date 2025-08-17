using Content.Shared.Roles;

namespace Content.Server._Harmony.Roles;

/// <summary>
///     Added to mind role entities to tag that they are a malfunctioning AI.
/// </summary>
[RegisterComponent]
public sealed partial class MalfunctioningAIRoleComponent : BaseMindRoleComponent
{
    [DataField] public EntityUid? Action;
    [DataField] public int OverloadMachineDetonationTime = 3; // time for Overload Machine do-after
}
