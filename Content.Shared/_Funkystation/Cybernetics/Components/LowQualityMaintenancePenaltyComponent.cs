namespace Content.Shared.Cybernetics.Components;

/// <summary>
/// Marker component on cyber limbs that have the +2 penalty from using a normal screwdriver during wire repair.
/// Removable by redoing the repair with a precision screwdriver.
/// </summary>
[RegisterComponent]
public sealed partial class LowQualityMaintenancePenaltyComponent : Component;
