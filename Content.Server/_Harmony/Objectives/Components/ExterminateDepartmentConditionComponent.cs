using Content.Server._Harmony.Objectives.Systems;

namespace Content.Server._Harmony.Objectives.Components;

/// <summary>
/// Requires that a department dies.
/// </summary>
[RegisterComponent, Access(typeof(ExterminateDepartmentConditionSystem))]
public sealed partial class ExterminateDepartmentConditionComponent : Component
{
    /// <summary>
    /// The department to exterminate; one of "Command", "Security", "Medical", "Engineering", "Supply", "Service", "Science". Defaults to Command.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Department = "Command";
}
