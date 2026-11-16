using Content.Shared._Harmony.SignalJammer;
using Content.Shared.Doors.Components;

namespace Content.Shared.Silicons.StationAi;

public abstract partial class SharedStationAiSystem
{
    [Dependency] private SharedSignalJammerSystem _signalJammer = default!;

    private bool IsSignalJammedDoor(EntityUid? target)
    {
        return target != null
            && HasComp<DoorComponent>(target)
            && _signalJammer.IsCovered(target.Value, SignalJammerEffect.DoorControl);
    }
}
