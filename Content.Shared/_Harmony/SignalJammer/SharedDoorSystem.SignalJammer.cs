using Content.Shared._Harmony.SignalJammer;
using Content.Shared.Silicons.StationAi;

namespace Content.Shared.Doors.Systems;

public abstract partial class SharedDoorSystem
{
    [Dependency] private SharedSignalJammerSystem _signalJammer = default!;

    private bool SuppressSignalJammedAiClick(EntityUid door, EntityUid user)
    {
        if (!HasComp<StationAiHeldComponent>(user))
            return false;

        // A client cannot predict coverage from a jammer concealed in inventory
        return !_net.IsServer || _signalJammer.IsCovered(door, SignalJammerEffect.DoorControl);
    }
}
