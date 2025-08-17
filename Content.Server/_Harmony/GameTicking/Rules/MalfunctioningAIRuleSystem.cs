using Content.Server.Actions;
using Content.Server.AlertLevel;
using Content.Server.Antag;
using Content.Server.Audio;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Radio.Components;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Silicons.StationAi;
using Content.Server.Silicons.Laws;
using Content.Server.Station.Systems;
using Content.Server.Store.Systems;
using Content.Server._Harmony.GameTicking.Rules.Components;
using Content.Server._Harmony.Roles;
using Content.Shared.Audio;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station.Components;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared._Harmony.Malfunction;
using Content.Shared._Harmony.Malfunction.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Server._Harmony.Malfunction.Components;
using Content.Shared.Humanoid;

namespace Content.Server._Harmony.GameTicking.Rules;

public sealed class MalfunctioningAIRuleSystem : GameRuleSystem<MalfunctioningAIRuleComponent>
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedStationAiSystem _sharedAi = default!;
    [Dependency] private readonly StationAiSystem _ai = default!;
    [Dependency] private readonly SiliconLawSystem _lawSystem = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly ServerGlobalSoundSystem _sound = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;

    private const string MalfShopId = "ActionMalfShop";
    private const string MalfHackApcId = "ActionMalfHackApc";
    private static readonly ProtoId<CurrencyPrototype> CpuCurrencyPrototype = "CPU";

    /// <summary>
    ///     Logic ripped from NukeSystem for the Doomsday device.
    /// </summary>
    private float _nukeSongLength;
    private ResolvedSoundSpecifier _selectedNukeSong = String.Empty;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MalfunctioningAIRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);
        SubscribeLocalEvent<MalfunctioningAIRoleComponent, GetBriefingEvent>(OnGetBriefing);
        SubscribeLocalEvent<StoreComponent, MalfShopActionEvent>(OnShop);
        SubscribeLocalEvent<MalfHackApcActionEvent>(OnApcHacked);
        SubscribeLocalEvent<MalfOverloadMachineActionEvent>(OnOverloadAttempt);
        SubscribeLocalEvent<PendingOverloadComponent, MalfOverloadMachineFinishedEvent>(OnOverloadFinished);
        SubscribeLocalEvent<MalfOverrideAiaActionEvent>(OnOverrideAia);
        SubscribeLocalEvent<MalfunctioningAIRoleComponent, MalfDoomsdayStartEvent>(OnDoomsdayStart);

    }

    public bool IsAIDeactivated(EntityUid uid)
    {
        return HasComp<IntellicardedComponent>(uid) || !HasComp<StationMemberComponent>(Transform(uid).GridUid);
    }

    // Greeting upon MalfunctioningAI activation
    private void AfterAntagSelected(Entity<MalfunctioningAIRuleComponent> mindId, ref AfterAntagEntitySelectedEvent args)
    {
        mindId.Comp.Active = true; // If Active is true, that means there is an active malfunctioning AI. As a result, silicons will gain a zeroth law.
        var ent = args.EntityUid;
        EnsureComp<MalfunctioningAIRoleComponent>(ent);
        if (TryComp<IntrinsicRadioTransmitterComponent>(ent, out var transmitter))
            transmitter.Channels.Add("Syndicate");
        if (TryComp<ActiveRadioComponent>(ent, out var receiver))
            receiver.Channels.Add("Syndicate");

        _action.AddAction(ent, MalfShopId);
        _action.AddAction(ent, MalfHackApcId);

        // Send antagonist briefing to and update all cyborgs appropriately
        foreach (var lawComp in EntityQuery<SiliconLawProviderComponent>())
        {
            var silicon = lawComp.Owner;
            if (HasComp<NonMalfunctioningComponent>(silicon))
                continue;
            if (lawComp.Lawset == null)
                continue;
            _lawSystem.SetLaws(lawComp.Lawset.Laws, silicon, new SoundPathSpecifier("/Audio/_Harmony/Misc/malf_start.ogg"));
        }
    }

    // Character screen briefing
    private void OnGetBriefing(Entity<MalfunctioningAIRoleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;

        if (ent is null)
            return;
        args.Append(Loc.GetString("malf-role-greeting"));
    }

    private void OnShop(EntityUid uid, StoreComponent component, MalfShopActionEvent args)
    {
        if (IsAIDeactivated(uid)) return;
        _store.ToggleUi(args.Performer, uid, component);
    }

    private void OnApcHacked(MalfHackApcActionEvent args)
    {
        if (IsAIDeactivated(args.Performer)) return;
        if (args.Handled) return;
        args.Handled = true;
        if (TryComp<ApcComponent>(args.Target, out var apc))
        {
            if (apc.Hacked)
                return;
            _store.TryAddCurrency(new() { { CpuCurrencyPrototype, 20 } }, args.Performer);
            apc.Hacked = true;
            _popup.PopupEntity(Loc.GetString("malf-apc-hacked"), args.Target, PopupType.MediumCaution);
        }
    }

    private void OnOverloadAttempt(MalfOverloadMachineActionEvent args)
    {
        if (IsAIDeactivated(args.Performer)) return;
        if (!TryComp<MalfunctioningAIRoleComponent>(args.Performer, out var component)) return;
        if (args.Handled) return;
        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.Performer, TimeSpan.FromSeconds(component.OverloadMachineDetonationTime), new MalfOverloadMachineFinishedEvent(), args.Target, args.Target)
        {
            BreakOnDamage = false,
            BreakOnMove = false,
            NeedHand = false,
        };

        if (!TryComp<ApcPowerReceiverComponent>(args.Target, out var targetComp) || !targetComp.Powered)
        {
            _popup.PopupEntity(Loc.GetString("malf-machine-overload-not-powered"), args.Target, args.Performer);
            return;
        }

        if (HasComp<StationAiCoreComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("malf-must-prevent-deactivation"), args.Target, args.Performer); // no overloading yourself
            return;
        }

        if (HasComp<PendingOverloadComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("malf-already-overloading", ("machine", Identity.Entity(args.Target, EntityManager))), args.Target, args.Performer);
            return;
        }

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("malf-machine-overloaded", ("machine", Identity.Entity(args.Target, EntityManager))), args.Target, args.Performer, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("malf-machine-overloaded-others", ("machine", Identity.Entity(args.Target, EntityManager))), args.Target, Filter.PvsExcept(args.Performer), true, PopupType.MediumCaution);

        AddComp<PendingOverloadComponent>(args.Target);
    }

    private void OnOverrideAia(MalfOverrideAiaActionEvent args)
    {
        if (IsAIDeactivated(args.Performer)) return;
        if (args.Handled) return;
        args.Handled = true;

        if (!TryComp<StationAiWhitelistComponent>(args.Target, out var whitelistComp)) return;
        _popup.PopupEntity(Loc.GetString("malf-access-override", ("machine", Identity.Entity(args.Target, EntityManager))), args.Target, args.Performer);

        EntityManager.System<SharedStationAiSystem>()
            .SetWhitelistEnabled((args.Target, whitelistComp), true);
    }

    private void OnOverloadFinished(EntityUid uid, PendingOverloadComponent component, MalfOverloadMachineFinishedEvent args)
    {
        _explosion.QueueExplosion(uid, component.ExplosionType, component.TotalIntensity, component.Slope, component.MaxTileIntensity);
        // QueueDel(uid);
    }

    private void OnDoomsdayStart(EntityUid uid, MalfunctioningAIRoleComponent component, MalfDoomsdayStartEvent args) // a ton of Doomsday logic is ripped from nuke logic as they are very similar
    {
        if (HasComp<DoomsdayComponent>(uid)) return; // you can't activate multiple doomsday devices at a time
        if (IsAIDeactivated(uid)) return;

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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DoomsdayComponent>();
        while (query.MoveNext(out var uid, out var doomsday))
        {
            if (IsAIDeactivated(uid))
                AvertDoomsday(uid, doomsday); // cancel the doomsday device if the AI is detached or carded

            TickTimer(uid, frameTime, doomsday);
        }
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
            DoomsdayActivate(uid, doomsday);
        }
    }

    private void DoomsdayActivate(EntityUid uid, DoomsdayComponent? doomsday = null)
    {
        var query = EntityQueryEnumerator<MalfunctioningAIRuleComponent>();
        while (query.MoveNext(out var comp))
            comp.DoomsdayActivated = true;

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
