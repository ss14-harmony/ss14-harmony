namespace Content.Shared.Medical.Integrity.Components;

/// <summary>
/// Marker component for entities that provide a sterile surface for surgery (e.g. operating table).
/// When present on a tile, reduces unsanitary surgery penalty.
/// </summary>
[RegisterComponent]
public sealed partial class SterileSurgerySurfaceComponent : Component
{
}
