using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Marks an implanted foot organ as carrying paraplegia from character traits / cloning.
/// Mechanical mobility is derived in <see cref="LimbDetachmentEffectsSystem.UpdateFeetMovement"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FootTraitParaplegicComponent : Component
{
}
