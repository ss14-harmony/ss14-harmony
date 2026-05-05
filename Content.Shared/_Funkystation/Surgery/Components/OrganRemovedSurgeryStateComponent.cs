using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Stores the removal procedure steps that were performed when this organ was surgically detached.
/// When re-inserting, only insertion repair steps are shown; removal steps are skipped since they were already done.
/// </summary>
[NetworkedComponent]
[RegisterComponent]
[AutoGenerateComponentState]
public sealed partial class OrganRemovedSurgeryStateComponent : Component
{
    /// <summary>
    /// Step IDs that were performed during removal (e.g. OrganClampVessels, OrganRemovalScalpel, OrganRemovalHemostat, RemoveOrgan).
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> PerformedRemovalSteps { get; set; } = new();
}
