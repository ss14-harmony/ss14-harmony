using Content.Shared._Harmony.SignalJammer;

namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraMonitorSystem
{
    [Dependency] private SharedSignalJammerSystem _signalJammer = default!;
    private readonly HashSet<EntityUid> _signalJammedMonitors = new();

    private bool CameraIsSignalJammed(SurveillanceCameraMonitorComponent monitor)
    {
        return monitor.ActiveCamera is { } camera
            && _signalJammer.IsCovered(camera, SignalJammerEffect.Camera);
    }

    private void UpdateSignalJamming(EntityUid uid, SurveillanceCameraMonitorComponent monitor)
    {
        var jammed = CameraIsSignalJammed(monitor);
        if (jammed == _signalJammedMonitors.Contains(uid))
            return;

        if (jammed)
        {
            _signalJammedMonitors.Add(uid);
            if (monitor.ActiveCamera is { } camera)
                _surveillanceCameras.RemoveActiveViewers(camera, monitor.Viewers, uid);
        }
        else
        {
            _signalJammedMonitors.Remove(uid);
            if (monitor.ActiveCamera is { } camera)
                _surveillanceCameras.AddActiveViewers(camera, monitor.Viewers, uid);
        }

        UpdateUserInterface(uid, monitor);
    }
}
