using Content.Server.Ninja.Systems;
using Content.Server.Power.Components;
using Content.Shared.CombatMode;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Ninja.Components;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Cybernetics.Systems;

/// <summary>
/// Allows users with cyber limbs to recharge batteries from APCs/substations/SMESes via empty-hand interaction.
/// </summary>
public sealed class CyberneticsBatteryDrainerSystem : EntitySystem
{
    private const float DrainTime = 1f;
    private const float DrainEfficiency = 0.0005f;

    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly CyberLimbModuleSystem _moduleSystem = default!;
    [Dependency] private readonly CyberLimbStatsSystem _cyberLimbStats = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbStatsComponent, BeforeInteractHandEvent>(OnBeforeInteractHand, after: [typeof(BatteryDrainerSystem)]);
        SubscribeLocalEvent<CyberLimbStatsComponent, DoAfterAttemptEvent<CyberneticsDrainDoAfterEvent>>(OnDoAfterAttempt);
        SubscribeLocalEvent<CyberLimbStatsComponent, CyberneticsDrainDoAfterEvent>(OnDoAfter);
    }

    private void OnBeforeInteractHand(Entity<CyberLimbStatsComponent> ent, ref BeforeInteractHandEvent args)
    {
        var (uid, stats) = ent;
        var target = args.Target;

        if (args.Handled || stats.BatteryMax <= 0 || !HasComp<PowerNetworkBatteryComponent>(target))
            return;

        if (_combatMode.IsInCombatMode(uid) || _hands.GetActiveItem(uid) != null || !_interaction.InRangeUnobstructed(uid, target))
            return;

        if (stats.BatteryRemaining >= stats.BatteryMax)
        {
            // Skip popup when ninja suit battery is full (avoids double popup when ninja+cyber both full)
            if (!(TryComp<BatteryDrainerComponent>(uid, out var d) && d.BatteryUid is {} bat && _battery.IsFull(bat)))
                _popup.PopupEntity(Loc.GetString("cybernetics-drainer-full"), uid, uid, PopupType.Medium);
            args.Handled = true;
            return;
        }

        args.Handled = true;

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, DrainTime, new CyberneticsDrainDoAfterEvent(), eventTarget: uid, target: target)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = false,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfterAttempt(Entity<CyberLimbStatsComponent> ent, ref DoAfterAttemptEvent<CyberneticsDrainDoAfterEvent> args)
    {
        var stats = ent.Comp;
        if (stats.BatteryMax <= 0 || stats.BatteryRemaining >= stats.BatteryMax)
        {
            args.Cancel();
            return;
        }

        var batteries = _moduleSystem.GetBatteryEntities(ent.Owner);
        if (batteries.Count == 0)
            args.Cancel();
    }

    private void OnDoAfter(Entity<CyberLimbStatsComponent> ent, ref CyberneticsDrainDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not {} target)
            return;

        var body = ent.Owner;
        var batteries = _moduleSystem.GetBatteryEntities(body);
        if (batteries.Count == 0)
        {
            args.Repeat = false;
            return;
        }

        if (!TryComp<BatteryComponent>(target, out var targetBattery) || !TryComp<PowerNetworkBatteryComponent>(target, out var pnb))
        {
            args.Repeat = false;
            return;
        }

        var available = _battery.GetCharge((target, targetBattery));
        if (MathHelper.CloseToPercent(available, 0))
        {
            _popup.PopupEntity(Loc.GetString("cybernetics-drainer-empty", ("target", target)), body, body, PopupType.Medium);
            args.Repeat = false;
            return;
        }

        var required = 0f;
        foreach (var batteryEnt in batteries)
        {
            if (TryComp<BatteryComponent>(batteryEnt, out var batteryComp))
                required += batteryComp.MaxCharge - _battery.GetCharge((batteryEnt, batteryComp));
        }

        var maxDrained = pnb.MaxSupply * DrainTime;
        var input = Math.Min(Math.Min(available, required / DrainEfficiency), maxDrained);
        if (!_battery.TryUseCharge((target, targetBattery), input))
        {
            args.Repeat = false;
            return;
        }

        var output = input * DrainEfficiency;
        var remaining = output;
        foreach (var batteryEnt in batteries)
        {
            if (remaining <= 0)
                break;
            if (!TryComp<BatteryComponent>(batteryEnt, out var batteryComp))
                continue;
            var needed = batteryComp.MaxCharge - _battery.GetCharge((batteryEnt, batteryComp));
            var toAdd = Math.Min(remaining, needed);
            _battery.ChangeCharge((batteryEnt, batteryComp), toAdd);
            remaining -= toAdd;
        }

        _cyberLimbStats.RecomputeAndRefresh(body);
        Spawn("EffectSparks", Transform(target).Coordinates);
        _audio.PlayPvs(new SoundCollectionSpecifier("sparks"), target);
        _popup.PopupEntity(Loc.GetString("cybernetics-drainer-success", ("target", target)), body, body);

        var anyNotFull = false;
        foreach (var batteryEnt in batteries)
        {
            if (TryComp<BatteryComponent>(batteryEnt, out var batteryComp) && !_battery.IsFull((batteryEnt, batteryComp)))
            {
                anyNotFull = true;
                break;
            }
        }
        args.Repeat = anyNotFull;
    }
}
