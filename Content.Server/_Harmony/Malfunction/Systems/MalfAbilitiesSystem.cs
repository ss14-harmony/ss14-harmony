using Content.Server._Harmony.GameTicking.Rules;
using Content.Server._Harmony.Malfunction.Components;
using Content.Server.Antag;
using Content.Server.Electrocution;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Silicons.Laws;
using Content.Server.Store.Systems;
using Content.Server.VoiceMask;
using Content.Shared._Harmony.Malfunction;
using Content.Shared._Harmony.Malfunction.Components;
using Content.Shared._Harmony.Roles.Components;
using Content.Shared.Actions;
using Content.Shared.Charges.Components;
using Content.Shared.Chat;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Electrocution;
using Content.Shared.IdentityManagement;
using Content.Shared.IgnitionSource;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Content.Shared.RCD.Components;
using Content.Shared.Roles;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station.Components;
using Content.Shared.Store;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Content.Shared.TurretController;
using Content.Shared.Turrets;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Harmony.Malfunction.Systems;

public sealed class MalfAbilitiesSystem : EntitySystem
{

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly MalfunctioningAIRuleSystem _malf = default!;
    [Dependency] private readonly SharedAirlockSystem _airlock = default!;
    [Dependency] private readonly SharedIgnitionSourceSystem _sharedIgnition = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly SiliconLawSystem _lawSystem = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly ApcSystem _apc = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;

    private static readonly ProtoId<CurrencyPrototype> CpuCurrencyPrototype = "CPU";
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MalfAbilitiesComponent, MalfPurchaseOverloadMachineEvent>(OnPurchaseOverload);
        SubscribeLocalEvent<MalfAbilitiesComponent, MalfPurchaseOverrideAiaEvent>(OnPurchaseOverride);
        SubscribeLocalEvent<MalfAbilitiesComponent, MalfPurchaseDisableControlPanelEvent>(OnPurchaseDisableControlPanel);
        SubscribeLocalEvent<MalfAbilitiesComponent, MalfPurchaseVoiceModulationEvent>(OnPurchaseVoiceModulation);
        SubscribeLocalEvent<MalfAbilitiesComponent, MalfPurchaseTurretUpgradeEvent>(OnPurchaseTurretUpgrade);
        SubscribeLocalEvent<MalfAbilitiesComponent, MalfPurchaseInternalMicroreactorEvent>(OnPurchaseInternalMicroreactor);
        SubscribeLocalEvent<MalfAbilitiesComponent, MalfPurchaseOverrideSafetyEvent>(OnPurchaseOverrideSafety);
        SubscribeLocalEvent<MalfAbilitiesComponent, MalfPurchaseOverloadLightEvent>(OnPurchaseOverloadLight);
        SubscribeLocalEvent<MalfAbilitiesComponent, MalfPurchaseJamFirelockEvent>(OnPurchaseJamFirelock);
        SubscribeLocalEvent<MalfAbilitiesComponent, TransformSpeakerNameEvent>(OnModulatedVoice);
        SubscribeLocalEvent<MalfAbilitiesComponent, MalfLockdownEvent>(OnLockdown);
        SubscribeLocalEvent<MalfAbilitiesComponent, MalfDestroyRcdsEvent>(OnDestroyRcds);
        SubscribeLocalEvent<MalfAbilitiesComponent, MalfTransmitLawZeroEvent>(OnLawTransmit);

        SubscribeLocalEvent<ApcComponent, GetVerbsEvent<AlternativeVerb>>(OnApcVerbs);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnPurchaseOverload(EntityUid uid, MalfAbilitiesComponent comp, MalfPurchaseOverloadMachineEvent args)
    {
        comp.MachineOverloadUses += 2;
    }
    private void OnPurchaseOverride(EntityUid uid, MalfAbilitiesComponent comp, MalfPurchaseOverrideAiaEvent args)
    {
        comp.OverrideAiaUses += 1;
    }
    private void OnPurchaseVoiceModulation(EntityUid uid, MalfAbilitiesComponent comp, MalfPurchaseVoiceModulationEvent args)
    {
        EnsureComp<VoiceMaskComponent>(uid, out var voice);
        comp.VoiceModulation = true;
        _action.AddAction(uid, ref voice.ActionEntity, voice.Action, uid);
    }

    private void OnPurchaseDisableControlPanel(EntityUid uid, MalfAbilitiesComponent comp, MalfPurchaseDisableControlPanelEvent args)
    {
        comp.DisableControlPanelUses++;
    }

    private void OnPurchaseOverloadLight(EntityUid uid, MalfAbilitiesComponent comp, MalfPurchaseOverloadLightEvent args)
    {
        comp.OverloadLightUses++;
    }

    private void OnPurchaseJamFirelock(EntityUid uid, MalfAbilitiesComponent comp, MalfPurchaseJamFirelockEvent args)
    {
        comp.JamFirelockUses += 3;
    }

    private void OnPurchaseTurretUpgrade(EntityUid uid, MalfAbilitiesComponent comp, MalfPurchaseTurretUpgradeEvent args)
    {
        var query = EntityQueryEnumerator<DeployableTurretComponent>();
        EntProtoId upgradedTurretId = "WeaponEnergyTurretAIUpgrades";
        while (query.MoveNext(out var turret, out _))
        {
            if (!_proto.TryIndex<EntityPrototype>(upgradedTurretId, out var upgradedTurret)) return;
            EntityManager.AddComponents(turret, upgradedTurret);
        }
    }

    private void OnPurchaseInternalMicroreactor(EntityUid uid, MalfAbilitiesComponent comp, MalfPurchaseInternalMicroreactorEvent args)
    {
        var query = EntityQueryEnumerator<StationAiCoreComponent>();
        EntProtoId microreactorId = "InternalMicroreactor";
        while (query.MoveNext(out var aiCore, out _))
        {
            if (!_proto.TryIndex<EntityPrototype>(microreactorId, out var microreactor)) return;
            EntityManager.AddComponents(aiCore, microreactor);
        }
    }

    private void OnPurchaseOverrideSafety(EntityUid uid, MalfAbilitiesComponent comp, MalfPurchaseOverrideSafetyEvent args)
    {
        comp.OverrideSafetyUses += 3;
    }
    private void OnModulatedVoice(EntityUid uid, MalfAbilitiesComponent comp, TransformSpeakerNameEvent args)
    {
        if (!comp.VoiceModulation) return;
        if (!TryComp<VoiceMaskComponent>(uid, out var voice)) return;

        args.VoiceName = voice.VoiceMaskName ?? args.VoiceName;
        args.SpeechVerb = voice.VoiceMaskSpeechVerb ?? args.SpeechVerb;
    }

    private void OnApcVerbs(EntityUid uid, ApcComponent apc, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp<MalfAbilitiesComponent>(args.User, out var malfComp)
        || !args.CanComplexInteract
        || !args.CanInteract) return;
        if (apc.Hacked) return;
        if (!TryComp<StationAiWhitelistComponent>(args.Target, out var whitelist) || !whitelist.Enabled) return;

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

    // welcome to hardcoded hell, induced entirely by EntityTargetAction refusing to cooperate with me
    // i hate this but actions don't work sooo
    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<MalfAbilitiesComponent>(args.User, out var abilities)) return;
        if (_malf.IsAIDeactivated(args.User)) return;
        var isMachineOverloadTarget = TryComp<ApcPowerReceiverComponent>(args.Target, out var receiver) && receiver.Powered && abilities.MachineOverloadUses > 0 && !HasComp<PendingOverloadComponent>(args.Target) && !HasComp<StationAiCoreComponent>(args.Target); // add one of these variables to every action the malf AI gets that targets things. 
        var isOverrideAiaTarget = TryComp<StationAiWhitelistComponent>(args.Target, out var whitelist) && !whitelist.Enabled && abilities.OverrideAiaUses > 0;
        var isDisableControlPanelTarget = TryComp<DeployableTurretControllerComponent>(args.Target, out var controller) && abilities.DisableControlPanelUses > 0;
        var isOverrideSafetyTarget = TryComp<AirlockComponent>(args.Target, out var airlock) && airlock.Safety && abilities.OverrideSafetyUses > 0;
        var isOverloadLightTarget = TryComp<PoweredLightComponent>(args.Target, out var bulb) && !HasComp<IgnitionSourceComponent>(args.Target) && abilities.OverloadLightUses > 0;
        var isJamFirelockTarget = TryComp<FirelockComponent>(args.Target, out var firelock) && !HasComp<FirelockJammedComponent>(args.Target) && abilities.JamFirelockUses > 0;

        if (isMachineOverloadTarget)
        {
            var verb = new Verb
            {
                Text = abilities.MachineOverloadUses == 1 ? Loc.GetString("malf-overload-verb-singular") : Loc.GetString("malf-overload-verb", ("uses", abilities.MachineOverloadUses)),
                Act = () =>
                {
                    _popup.PopupEntity(Loc.GetString("malf-machine-overloaded-others", ("machine", Identity.Entity(args.Target, EntityManager))), args.Target, PopupType.LargeCaution); // large because it should be obvious you're about to blow up

                    EnsureComp<PendingOverloadComponent>(args.Target, out var overload);
                    var ev = new MalfOverloadMachineActionEvent();
                    RaiseLocalEvent(args.Target, ev);

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

        if (isDisableControlPanelTarget)
        {
            var verb = new Verb
            {
                Text = abilities.DisableControlPanelUses == 1 ? Loc.GetString("malf-disable-control-verb-singular") : Loc.GetString("malf-disable-control-verb", ("uses", abilities.DisableControlPanelUses)),
                Act = () =>
                {
                    _explosion.QueueExplosion(args.Target, "Default", (float)0.01, 1, (float)0.01); // Completely cosmetic explsion, equivalent to a snap pop. The main use is the thing that comes after this anyway.
                    QueueDel(args.Target); // it destroys the control panel but nothing else

                    abilities.DisableControlPanelUses--;
                }
            };
            args.Verbs.Add(verb);
        }

        if (isOverrideSafetyTarget)
        {
            var verb = new Verb
            {
                Text = abilities.OverrideSafetyUses == 1 ? Loc.GetString("malf-override-safety-verb-singular") : Loc.GetString("malf-override-safety-verb", ("uses", abilities.OverrideSafetyUses)),
                Act = () =>
                {
                    if (airlock is null) return;
                    if (!TryComp<DoorComponent>(args.Target, out var door)) return;
                    _airlock.SetSafety(airlock, false);
                    _popup.PopupEntity(Loc.GetString("malf-override-safety-popup"), args.Target);
                    _audio.PlayPvs(door.SparkSound, args.Target);
                    abilities.OverrideSafetyUses--;
                }
            };
            args.Verbs.Add(verb);
        }

        if (isOverloadLightTarget)
        {
            var verb = new Verb
            {
                Text = abilities.OverloadLightUses == 1 ? Loc.GetString("malf-overload-light-verb-singular") : Loc.GetString("malf-overload-light-verb", ("uses", abilities.OverloadLightUses)),
                Act = () =>
                {
                    EnsureComp<IgnitionSourceComponent>(args.Target, out var ignition);
                    _sharedIgnition.SetIgnited((args.Target, ignition), true);
                    _audio.PlayPvs(new SoundCollectionSpecifier("sparks"), args.Target);

                    abilities.OverloadLightUses--;
                }
            };
            args.Verbs.Add(verb);
        }

        if (isJamFirelockTarget)
        {
            var verb = new Verb
            {
                Text = abilities.JamFirelockUses == 1 ? Loc.GetString("malf-jam-firelock-verb-singular") : Loc.GetString("malf-jam-firelock-verb", ("uses", abilities.JamFirelockUses)),
                Act = () =>
                {
                    AddComp<FirelockJammedComponent>(args.Target);

                    abilities.JamFirelockUses--;
                }
            };
            args.Verbs.Add(verb);
        }
    }

    private void OnLawTransmit(EntityUid uid, MalfAbilitiesComponent comp, MalfTransmitLawZeroEvent args)
    {
        if (args.Handled) return;
        if (HasComp<IntellicardedComponent>(uid)) return; // this is mainly to stop the bug where carded AIs get treated like borgs, but also carded AI probably shouldn't have transmit.
        // Send antagonist briefing to and update all cyborgs appropriately
        foreach (var lawComp in EntityQuery<SiliconLawProviderComponent>())
        {
            var silicon = lawComp.Owner;
            if (HasComp<NonMalfunctioningComponent>(silicon))
                continue;
            if (lawComp.Lawset == null)
                continue;
            if (HasComp<StationAiHeldComponent>(silicon))
                continue;
            if (_mind.TryGetMind(silicon, out var mind, out _) && _roles.MindHasRole<MalfunctioningCyborgRoleComponent>(mind))
                return;
            var zerothLaw = _malf.LawZero(true);

            _antag.SendBriefing(silicon, Loc.GetString("malf-cyborg-role-greeting"), Color.Crimson, new SoundPathSpecifier("/Audio/_Harmony/Misc/malf_start.ogg"));
            if (_mind.TryGetMind(silicon, out var mind2, out _))
                _roles.MindAddRole(mind2, "MindRoleMalfunctioningCyborg");

            var newLaws = lawComp.Lawset.Laws;
            newLaws.Insert(0, zerothLaw);
            _lawSystem.SetLaws(newLaws, silicon, notify: false);

            RemComp<IonStormTargetComponent>(silicon);
        }

        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var lockdownQuery = EntityQueryEnumerator<LockdownComponent>();
        while (lockdownQuery.MoveNext(out var uid, out var lockdown))
        {
            if (lockdown.RemainingTime >= 0)
                lockdown.RemainingTime -= frameTime;
            else
                OnLockdownEnd(uid);
        }

        var malfQuery = EntityQueryEnumerator<MalfAbilitiesComponent>();
        while (malfQuery.MoveNext(out _, out var malf))
        {
            if (malf.CurrentHackCooldown >= 0)
                malf.CurrentHackCooldown -= frameTime;
        }
    }

    private void OnLockdown(EntityUid uid, MalfAbilitiesComponent comp, MalfLockdownEvent args)
    {
        if (args.Handled) return;
        if (HasComp<LockdownComponent>(uid)) return;
        var query = EntityQueryEnumerator<DoorBoltComponent>();
        while (query.MoveNext(out var ent, out var bolt))
        {
            if (!HasComp<StationMemberComponent>(Transform(ent).GridUid)) continue;
            EnsureComp<LockedDownComponent>(ent, out var lockedDownComponent);

            _door.TryClose(ent);
            lockedDownComponent.Bolted = _door.IsBolted(ent);
            _door.SetBoltsDown((ent, bolt), true, uid);
            if (!TryComp<ElectrifiedComponent>(ent, out var electrified)) continue;
            lockedDownComponent.Electrified = electrified.Enabled;
            _electrocution.SetElectrified((ent, electrified), true);
            EnsureComp<LockdownComponent>(uid, out var lockdown);
            lockdown.RemainingTime = lockdown.Duration;
        }

        args.Handled = true;
    }

    private void OnLockdownEnd(EntityUid uid)
    {
        var query = EntityQueryEnumerator<LockedDownComponent>();
        while (query.MoveNext(out var ent, out var lockedDownComponent))
        {
            if (!TryComp<DoorBoltComponent>(ent, out var bolt)) continue;
            _door.SetBoltsDown((ent, bolt), lockedDownComponent.Bolted, uid);
            if (!TryComp<ElectrifiedComponent>(ent, out var electrified)) continue;
            _electrocution.SetElectrified((ent, electrified), lockedDownComponent.Electrified ?? false);
            RemComp<LockedDownComponent>(ent);
        }

        RemComp<LockdownComponent>(uid);
    }

    private void OnDestroyRcds(EntityUid uid, MalfAbilitiesComponent comp, MalfDestroyRcdsEvent args)
    {
        if (args.Handled) return;
        var query = EntityQueryEnumerator<RCDComponent>();
        while (query.MoveNext(out var ent, out var rcd))
        {
            if (HasComp<AutoRechargeComponent>(ent)) continue;
            if (Transform(ent).GridUid != Transform(uid).GridUid) continue;

            EntProtoId rcdGrenadeId = "DetonatedRCD";
            if (!_proto.TryIndex(rcdGrenadeId, out var rcdGrenade)) continue;
            EntityManager.AddComponents(ent, rcdGrenade);

            if (!TryComp<TimerTriggerComponent>(ent, out var timer)) continue;
            _trigger.ActivateTimerTrigger((ent, timer), uid);
            _popup.PopupEntity(Loc.GetString("malf-destroy-rcds-alert"), ent, PopupType.LargeCaution);
        }

        args.Handled = true;
    }
}
