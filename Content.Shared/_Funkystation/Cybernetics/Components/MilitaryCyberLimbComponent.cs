namespace Content.Shared.Cybernetics.Components;

/// <summary>
/// Marker component for military cyber limbs (variant stats / recipes).
/// Each attached military limb contributes 5% reduction (additive, capped at 100%) to incoming Brute (Blunt/Slash/Piercing)
/// and Heat damage; aggregated into <see cref="CyberLimbStatsComponent.CyberDamageResistance"/> by <see cref="CyberLimbStatsSystem"/>.
/// Structural plating — unaffected by power or maintenance state.
/// </summary>
[RegisterComponent]
public sealed partial class MilitaryCyberLimbComponent : Component
{
}
