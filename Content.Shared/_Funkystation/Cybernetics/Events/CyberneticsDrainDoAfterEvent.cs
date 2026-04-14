using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Cybernetics.Events;

/// <summary>
/// DoAfter event for cybernetics battery draining from power sources.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CyberneticsDrainDoAfterEvent : SimpleDoAfterEvent;
