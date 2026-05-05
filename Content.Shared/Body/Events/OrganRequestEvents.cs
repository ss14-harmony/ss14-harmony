using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Shared.Body.Events;

/// <summary>
/// Raised to request inserting an organ into a body part. Handled by BodyPartOrganSystem.
/// </summary>
[ByRefEvent]
public record struct OrganInsertRequestEvent(EntityUid BodyPart, EntityUid Organ)
{
    /// <summary>
    /// Whether the insert succeeded. Populated by BodyPartOrganSystem.
    /// </summary>
    public bool Success { get; set; }
}

/// <summary>
/// Raised to request removing an organ from its container. Handled by BodyPartOrganSystem.
/// </summary>
[ByRefEvent]
public record struct OrganRemoveRequestEvent(EntityUid Organ)
{
    /// <summary>
    /// Where to place the organ after removal. If null, uses default AttachParentToContainerOrGrid behavior.
    /// </summary>
    public EntityCoordinates? Destination;

    /// <summary>
    /// Optional local rotation after removal. Used for limb drop placement when patient is laying down.
    /// </summary>
    public Angle? LocalRotation;

    /// <summary>
    /// Whether the remove succeeded. Populated by BodyPartOrganSystem.
    /// </summary>
    public bool Success { get; set; }
}
