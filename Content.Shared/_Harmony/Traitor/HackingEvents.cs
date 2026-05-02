using Content.Shared._Harmony.Traitor.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Harmony.Traitor;

/// <summary>
/// Event raised on the target entity when a hacking beacon is planted onto it.
/// </summary>
public sealed class StructureHackedEvent : EntityEventArgs;

/// <summary>
/// Event raised by another system when criteria is met when a hacking beacon is planted on an entity.
/// </summary>
public sealed class StructureHackCompletedEvent : EntityEventArgs;

/// <summary>
/// Event raised on the target entity when a hacking beacon is unstuck from it.
/// </summary>
public sealed class BeaconRemovedEvent : EntityEventArgs;

/// <summary>
/// Event raised on the target entity when an attempt to stick a hacking beacon on an entity is made. Can be cancelled.
/// </summary>
public sealed class AttemptHackStructureEvent : CancellableEntityEventArgs
{
    public bool Repeat = false; // if this isn't the first time we've hacked a repeatably hackable structure
};

/// <summary>
/// Raised in the hacking beacon update loop on the object it is attached to until StopUpdating is set to true.
/// </summary>
public sealed class HackUpdateEvent : EntityEventArgs
{
    public TimeSpan NextUpdate; // when will we call this next?
    public Entity<ActiveHackingBeaconComponent> Beacon; // the hacking beacon calling this event
    public bool CompleteHack = false; // should we raise StructureHackCompletedEvent? this will stop the entity from receiving further update events.
};
