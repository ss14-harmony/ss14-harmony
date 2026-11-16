using Content.Shared._Harmony.SignalJammer;

namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraSystem
{
    [Dependency] private SharedSignalJammerSystem _signalJammer = default!;

    private bool IsSignalJammed(EntityUid camera)
    {
        return _signalJammer.IsCovered(camera, SignalJammerEffect.Camera);
    }
}
