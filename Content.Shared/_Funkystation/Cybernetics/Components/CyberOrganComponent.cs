using Robust.Shared.Prototypes;

namespace Content.Shared.Cybernetics.Components;

/// <summary>
/// Marker component for cyber organs (heart, lungs, liver, stomach, eyes).
/// Entities with this are cyber organs - they do NOT use CyberLimb (no power, maintenance, storage, or modules).
/// </summary>
[RegisterComponent]
public sealed partial class CyberOrganComponent : Component
{
    /// <summary>
    /// Effectiveness multiplier: 0.8 (Basic), 1.0 (T1), 1.2 (T2), or 1.4 (T3).
    /// Used by organ-specific systems to scale behavior.
    /// </summary>
    [DataField]
    public float Effectiveness { get; set; } = 1f;
}
