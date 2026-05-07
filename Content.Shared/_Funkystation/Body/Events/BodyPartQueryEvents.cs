using System.Collections.Generic;
using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body.Events;

/// <summary>
/// Raised to get all body parts for a body. Response is populated in <see cref="Parts"/>.
/// </summary>
[ByRefEvent]
public record struct BodyPartQueryEvent(EntityUid Body)
{
    /// <summary>
    /// The body parts (torso, head, arms, legs) belonging to the body. Populated by BodySystem.
    /// </summary>
    public List<EntityUid> Parts { get; set; } = new();
}

/// <summary>
/// Raised to get body parts filtered by category and/or symmetry. Response is populated in <see cref="Parts"/>.
/// </summary>
[ByRefEvent]
public record struct BodyPartQueryByTypeEvent(EntityUid Body)
{
    /// <summary>
    /// Optional filter by organ category (e.g. Torso, ArmLeft, Lungs).
    /// </summary>
    public ProtoId<OrganCategoryPrototype>? Category { get; set; }

    /// <summary>
    /// Optional filter by symmetry. Left = ArmLeft, HandLeft, etc.; Right = ArmRight, HandRight, etc.
    /// </summary>
    public BodyPartSymmetry? Symmetry { get; set; }

    /// <summary>
    /// The matching body parts. Populated by BodySystem.
    /// </summary>
    public List<EntityUid> Parts { get; set; } = new();
}

/// <summary>
/// Symmetry of a body part (left, right, or neither).
/// </summary>
public enum BodyPartSymmetry
{
    None,
    Left,
    Right
}
