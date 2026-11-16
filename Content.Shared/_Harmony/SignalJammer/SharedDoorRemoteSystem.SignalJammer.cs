using Content.Shared._Harmony.SignalJammer;
using Robust.Shared.Network;

namespace Content.Shared.Remotes.EntitySystems;

public abstract partial class SharedDoorRemoteSystem
{
    [Dependency] private SharedSignalJammerSystem _signalJammer = default!;
    [Dependency] private INetManager _signalJammerNet = default!;

    private bool IsSignalJammed(EntityUid remote, EntityUid door)
    {
        return _signalJammer.IsDoorRemoteJammed(remote, door);
    }
}
