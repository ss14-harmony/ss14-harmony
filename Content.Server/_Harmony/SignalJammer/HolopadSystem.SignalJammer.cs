using System.Linq;
using Content.Shared._Harmony.SignalJammer;
using Content.Shared.Holopad;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Telephone;

namespace Content.Server.Holopad;

public sealed partial class HolopadSystem
{
    [Dependency] private SharedSignalJammerSystem _signalJammer = default!;
    private readonly HashSet<EntityUid> _activeSignalJammerHolopads = new();

    private void TrackSignalJammerHolopad(EntityUid uid, TelephoneState state)
    {
        if (state is (TelephoneState.Calling or TelephoneState.Ringing or TelephoneState.InCall) &&
            !HasComp<StationAiCoreComponent>(uid))
            _activeSignalJammerHolopads.Add(uid);
        else
            _activeSignalJammerHolopads.Remove(uid);
    }

    private void UpdateSignalJammedHolopads()
    {
        // Telephone state events maintain this small set; no world-wide holopad scan is needed.
        foreach (var uid in _activeSignalJammerHolopads.ToArray())
        {
            if (!TryComp<TelephoneComponent>(uid, out var telephone) ||
                !HasComp<HolopadComponent>(uid))
            {
                _activeSignalJammerHolopads.Remove(uid);
                continue;
            }

            if (_signalJammer.IsCovered(uid, SignalJammerEffect.Camera))
                _telephoneSystem.TerminateTelephoneCalls((uid, telephone));
        }
    }
}
