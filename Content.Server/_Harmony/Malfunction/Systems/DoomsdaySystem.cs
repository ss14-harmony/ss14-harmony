using Content.Server.AlertLevel;
using Content.Server.Audio;
using Content.Server.Chat.Systems;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Server._Harmony.Malfunction.Components;
using Content.Server._Harmony.GameTicking.Rules;
using Content.Shared.Audio;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared._Harmony.Malfunction;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Harmony.Malfunction.Systems;

public sealed class DoomsdaySystem : EntitySystem
{
    [Dependency] private readonly MalfunctioningAIRuleSystem _malf = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly ServerGlobalSoundSystem _sound = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    /// <summary>
    ///     Logic ripped from NukeSystem for the Doomsday device.
    /// </summary>
    private float _nukeSongLength;
    private ResolvedSoundSpecifier _selectedNukeSong = String.Empty;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MalfAbilitiesComponent, MalfDoomsdayStartEvent>(OnDoomsdayStart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DoomsdayComponent>();
        while (query.MoveNext(out var uid, out var doomsday))
        {
            if (_malf.IsAIDeactivated(uid))
                AvertDoomsday(uid, doomsday); // cancel the doomsday device if the AI is detached or carded

            TickTimer(uid, frameTime, doomsday);
        }
    }

    private void OnDoomsdayStart(EntityUid uid, MalfAbilitiesComponent component, MalfDoomsdayStartEvent args) // a ton of Doomsday logic is ripped from nuke logic as they are very similar
    {
        if (HasComp<DoomsdayComponent>(uid)) return; // you can't activate multiple doomsday devices at a time
        if (_malf.IsAIDeactivated(uid)) return;

        if (args.Handled) return;
        args.Handled = true;

        var stationUid = _station.GetStationInMap(Transform(uid).MapID);

        EnsureComp<DoomsdayComponent>(uid, out var doomsdayComponent);
        doomsdayComponent.RemainingTime = doomsdayComponent.Timer;


        doomsdayComponent.InitialGrid = _station.GetStationInMap(Transform(uid).MapID);
        if (stationUid != null)
            _alertLevel.SetLevel(stationUid.Value, doomsdayComponent.AlertLevelOnActivate, true, true, true, true);

        // We are collapsing the randomness here, otherwise we would get separate random song picks for checking duration and when actually playing the song afterwards
        _selectedNukeSong = _audio.ResolveSound(doomsdayComponent.ArmMusic);

        var announcement = Loc.GetString("malf-doomsday-announcement",
        ("time", (int)doomsdayComponent.RemainingTime));
        var sender = Loc.GetString("malf-doomsday-announcement-sender");
        _chatSystem.DispatchStationAnnouncement(stationUid ?? uid, announcement, sender, false, null, Color.Crimson);

        _nukeSongLength = (float)_audio.GetAudioLength(_selectedNukeSong).TotalSeconds;
    }

    private void TickTimer(EntityUid uid, float frameTime, DoomsdayComponent? doomsday = null)
    {
        if (!Resolve(uid, ref doomsday))
            return;

        doomsday.RemainingTime -= frameTime;

        // Start playing the song
        // should play
        if (doomsday.RemainingTime <= _nukeSongLength && !doomsday.PlayedDoomsdaySong && !ResolvedSoundSpecifier.IsNullOrEmpty(_selectedNukeSong))
        {
            _sound.DispatchStationEventMusic(uid, _selectedNukeSong, StationEventMusicType.Nuke);
            doomsday.PlayedDoomsdaySong = true;
        }

        if (doomsday.RemainingTime <= 0)
        {
            doomsday.RemainingTime = 0;
            DoomsdayActivate(uid);
        }
    }

    private void DoomsdayActivate(EntityUid uid)
    {
        var ev = new MalfDoomsdayActivatedEvent();
        RaiseLocalEvent(ev);

        var crewQuery = EntityQueryEnumerator<HumanoidAppearanceComponent, TransformComponent>();
        while (crewQuery.MoveNext(out var ent, out _, out var transform))
        {
            if (!TryComp<BodyComponent>(ent, out var body))
                return;
            if (Transform(uid).MapID != transform.MapID) return;

            _body.GibBody(ent, true, body); // it just instantly gibs all humanoids on the same grid
        }

        _roundEndSystem.EndRound();
    }

    private void AvertDoomsday(EntityUid uid, DoomsdayComponent component)
    {
        var stationUid = component.InitialGrid;
        if (stationUid != null)
            _alertLevel.SetLevel(stationUid.Value, component.AlertLevelOnDeactivate, true, true, true);

        var announcement = Loc.GetString("malf-doomsday-aborted");
        var sender = Loc.GetString("malf-doomsday-announcement-sender");
        _chatSystem.DispatchStationAnnouncement(uid, announcement, sender, false);

        _sound.PlayGlobalOnStation(uid, _audio.ResolveSound(component.DisarmSound));
        _sound.StopStationEventMusic(uid, StationEventMusicType.Nuke);

        RemComp<DoomsdayComponent>(uid);
    }
}
