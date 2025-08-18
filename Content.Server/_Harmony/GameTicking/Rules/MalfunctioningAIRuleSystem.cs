using Content.Server.Actions;
using Content.Server.AlertLevel;
using Content.Server.Antag;
using Content.Server.Audio;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Radio.Components;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Silicons.StationAi;
using Content.Server.Silicons.Laws;
using Content.Server.Station.Systems;
using Content.Server.Store.Systems;
using Content.Server._Harmony.GameTicking.Rules.Components;
using Content.Server._Harmony.Malfunction.Components;
using Content.Server._Harmony.Roles;
using Content.Shared.Audio;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station.Components;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Verbs;
using Content.Shared._Harmony.Malfunction;
using Content.Shared._Harmony.Malfunction.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Server.Administration.Commands;
using Robust.Server.GameStates;

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
    [Dependency] private readonly ApcSystem _apc = default!;

    private const string MalfShopId = "ActionMalfShop";
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
        SubscribeLocalEvent<MalfAbilitiesComponent, MalfPurchaseOverloadMachineEvent>(OnPurchaseOverload);
        SubscribeLocalEvent<MalfAbilitiesComponent, MalfPurchaseOverrideAiaEvent>(OnPurchaseOverride);
        SubscribeLocalEvent<StoreComponent, MalfShopActionEvent>(OnShop);
        SubscribeLocalEvent<PendingOverloadComponent, MalfOverloadMachineFinishedEvent>(OnOverloadFinished);
        SubscribeLocalEvent<MalfunctioningAIRoleComponent, MalfDoomsdayStartEvent>(OnDoomsdayStart);
        SubscribeLocalEvent<ApcComponent, GetVerbsEvent<AlternativeVerb>>(OnApcVerbs);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    public bool IsAIDeactivated(EntityUid uid)
    {
        return HasComp<IntellicardedComponent>(uid) || !HasComp<StationMemberComponent>(Transform(uid).GridUid);
    }

    private void OnPurchaseOverload(EntityUid uid, MalfAbilitiesComponent comp, MalfPurchaseOverloadMachineEvent args)
    {
        comp.MachineOverloadUses += 2;
    }
    private void OnPurchaseOverride(EntityUid uid, MalfAbilitiesComponent comp, MalfPurchaseOverrideAiaEvent args)
    {
        comp.OverrideAiaUses += 1;
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
        if (args.Handled) return;
        _store.ToggleUi(args.Performer, uid, component);
        args.Handled = true;
    }

    private void OnOverloadFinished(EntityUid uid, PendingOverloadComponent component, MalfOverloadMachineFinishedEvent args)
    {
        _explosion.QueueExplosion(uid, component.ExplosionType, component.TotalIntensity, component.Slope, component.MaxTileIntensity);
        RemComp<PendingOverloadComponent>(uid);
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

        var malfQuery = EntityQueryEnumerator<MalfunctioningAIRoleComponent>();
        while (malfQuery.MoveNext(out var uid, out var malf))
        {
            if (malf.CurrentHackCooldown >= 0)
                malf.CurrentHackCooldown -= frameTime;
        }

        var overloadQuery = EntityQueryEnumerator<PendingOverloadComponent>();
        while (overloadQuery.MoveNext(out var uid, out var overload))
        {
            if (overload.TimeUntilDetonation >= 0)
                overload.TimeUntilDetonation -= frameTime;
            else
            {
                var ev = new MalfOverloadMachineFinishedEvent();
                RaiseLocalEvent(uid, ev);
            }
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
            DoomsdayActivate(uid);
        }
    }

    private void DoomsdayActivate(EntityUid uid)
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

    // welcome to hardcoded hell, induced entirely by EntityTargetAction refusing to cooperate with me
    // i hate this but actions don't work sooo
    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<MalfAbilitiesComponent>(args.User, out var abilities)) return;
        if (IsAIDeactivated(args.User)) return;
        var isMachineOverloadTarget = TryComp<ApcPowerReceiverComponent>(args.Target, out var receiver) && receiver.Powered && abilities.MachineOverloadUses > 0 && !HasComp<PendingOverloadComponent>(args.Target) && !HasComp<StationAiCoreComponent>(args.Target); // add one of these variables to every action the malf AI gets that targets things. 
        var isOverrideAiaTarget = TryComp<StationAiWhitelistComponent>(args.Target, out var whitelist) && !whitelist.Enabled && abilities.OverrideAiaUses > 0;

        if (isMachineOverloadTarget)
        {
            var verb = new Verb
            {
                Text = abilities.MachineOverloadUses == 1 ? Loc.GetString("malf-overload-verb-singular") : Loc.GetString("malf-overload-verb", ("uses", abilities.MachineOverloadUses)),
                Act = () =>
                {
                    _popup.PopupEntity(Loc.GetString("malf-machine-overloaded-others", ("machine", Identity.Entity(args.Target, EntityManager))), args.Target, PopupType.LargeCaution); // large because it should be obvious you're about to blow up

                    EnsureComp<PendingOverloadComponent>(args.Target, out var overload);
                    overload.TimeUntilDetonation = overload.DetonationDuration;
                    _audio.PlayPvs(overload.OverloadSound, args.Target); // alarm to notify anyone nearby it's about to explode
                    abilities.MachineOverloadUses--;
                }
            };
            args.Verbs.Add(verb);
        }

        if (isOverrideAiaTarget)
        {
            var verb = new Verb
            {
                Text = abilities.OverrideAiaUses == 1 ? Loc.GetString("malf-override-aia-verb-singular") : Loc.GetString("malf-override-aia-verb", ("uses", abilities.OverrideAiaUses)),
                Act = () =>
                {
                    if (whitelist is null) return;
                    EntityManager.System<SharedStationAiSystem>()
                    .SetWhitelistEnabled((args.Target, whitelist), true);
                    abilities.OverrideAiaUses--;
                }
            };
            args.Verbs.Add(verb);
        }
    }

    private void OnApcVerbs(EntityUid uid, ApcComponent apc, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp<MalfunctioningAIRoleComponent>(args.User, out var malfComp)
        || !args.CanComplexInteract
        || !args.CanInteract) return;
        if (apc.Hacked) return;

        var verb = new AlternativeVerb
        {
            Text = malfComp.CurrentHackCooldown >= 0 ? Loc.GetString("malf-hack-verb-cooldown", ("time", Math.Ceiling(malfComp.CurrentHackCooldown))) : Loc.GetString("malf-hack-verb"),
            Act = () =>
            {
                if (malfComp.CurrentHackCooldown >= 0) return;
                _store.TryAddCurrency(new() { { CpuCurrencyPrototype, 10 } }, args.User);
                apc.Hacked = true;
                _popup.PopupEntity(Loc.GetString("malf-apc-hacked"), args.Target, PopupType.MediumCaution);

                _apc.UpdateApcState(uid, apc);
                _audio.PlayPvs(malfComp.HackSound, uid);
                malfComp.CurrentHackCooldown = malfComp.HackApcTime;
            }
        };
        args.Verbs.Add(verb);
    }
}
