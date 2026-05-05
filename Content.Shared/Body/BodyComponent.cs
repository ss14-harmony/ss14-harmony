using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Body;

[RegisterComponent, NetworkedComponent]
[Access(typeof(BodySystem))]
public sealed partial class BodyComponent : Component
{
    public const string ContainerID = "body_organs";

    /// <summary>
    /// The actual container with entities with <see cref="OrganComponent" /> in it
    /// </summary>
    [ViewVariables]
    public Container? Organs;
}

/// <summary>
/// Raised on organ entity, when it is inserted into a body
/// </summary>
[ByRefEvent]
public readonly record struct OrganGotInsertedEvent(EntityUid Target);

/// <summary>
/// Raised on organ entity, when it is removed from a body
/// </summary>
[ByRefEvent]
public readonly record struct OrganGotRemovedEvent(EntityUid Target);

/// <summary>
/// Raised on body entity, when an organ is inserted into it
/// </summary>
[ByRefEvent]
public readonly record struct OrganInsertedIntoEvent(EntityUid Organ);

/// <summary>
/// Raised on body entity, when an organ is removed from it
/// </summary>
[ByRefEvent]
public readonly record struct OrganRemovedFromEvent(EntityUid Organ);

/// <summary>
/// Raised on the body immediately after <see cref="OrganRemovedFromEvent"/> with the same organ.
/// Use this when you need organ-removal reactions without consuming the single directed subscription slot for <see cref="OrganRemovedFromEvent"/>.
/// </summary>
[ByRefEvent]
public readonly record struct OrganRemovedFromBodyNotifyEvent(EntityUid Organ);

/// <summary>
/// Raised on a body after organ insert/remove paths in <see cref="BodySystem"/> update what is attached.
/// Used to reconcile glove/footwear inventory slots with anatomy.
/// </summary>
[ByRefEvent]
public readonly record struct AppendageWearInventoryRefreshEvent;
