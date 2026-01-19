using Content.Server.Ninja.Events;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Alert; // Harmony
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.Strip.Components; // Harmony
using Robust.Shared.Audio;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Ninja.Systems;

/// <summary>
/// Handles the doafter and power transfer when draining.
/// </summary>
public sealed class BatteryDrainerSystem : SharedBatteryDrainerSystem
{
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly PredictedBatterySystem _predictedBattery = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AlertsSystem _alertsSystem = default!; // Harmony start

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BatteryDrainerComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<BatteryDrainerComponent, NinjaBatteryChangedEvent>(OnBatteryChanged);
        // Harmony Start
        SubscribeLocalEvent<BatteryDrainerComponent, ToggleDrainingEvent>(OnToggleDraining);
        SubscribeLocalEvent<BatteryDrainerComponent, ComponentInit>(OnCompInit);
        SubscribeLocalEvent<BatteryDrainerComponent, ComponentRemove>(OnCompRemoved);
        // Harmony End
    }

    /// <summary>
    /// Start do after for draining a power source.
    /// Can't predict PNBC existing so only done on server.
    /// </summary>
    private void OnBeforeInteractHand(Entity<BatteryDrainerComponent> ent, ref BeforeInteractHandEvent args)
    {
        // Harmony Start
        if (ent.Comp.Draining == false)
            return;
        // Harmony End

        var (uid, comp) = ent;
        var target = args.Target;
        if (args.Handled || comp.BatteryUid is not { } battery || !HasComp<PowerNetworkBatteryComponent>(target))
            return;

        // handles even if battery is full so you can actually see the poup
        args.Handled = true;

        if (_battery.IsFull(battery))
        {
            _popup.PopupEntity(Loc.GetString("battery-drainer-full"), uid, uid, PopupType.Medium);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, comp.DrainTime, new DrainDoAfterEvent(), target: target, eventTarget: uid)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = false,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnBatteryChanged(Entity<BatteryDrainerComponent> ent, ref NinjaBatteryChangedEvent args)
    {
        SetBattery((ent, ent.Comp), args.Battery);
    }

    /// <inheritdoc/>
    protected override void OnDoAfterAttempt(Entity<BatteryDrainerComponent> ent, ref DoAfterAttemptEvent<DrainDoAfterEvent> args)
    {
        base.OnDoAfterAttempt(ent, ref args);

        if (ent.Comp.BatteryUid is not { } battery || _battery.IsFull(battery))
            args.Cancel();
    }

    /// <inheritdoc/>
    protected override bool TryDrainPower(Entity<BatteryDrainerComponent> ent, EntityUid target)
    {
        var (uid, comp) = ent;
        if (comp.BatteryUid == null || !TryComp<PredictedBatteryComponent>(comp.BatteryUid.Value, out var battery))
            return false;

        if (!TryComp<BatteryComponent>(target, out var targetBattery) || !TryComp<PowerNetworkBatteryComponent>(target, out var pnb))
            return false;

        if (MathHelper.CloseToPercent(targetBattery.CurrentCharge, 0))
        {
            _popup.PopupEntity(Loc.GetString("battery-drainer-empty", ("battery", target)), uid, uid, PopupType.Medium);
            return false;
        }

        var available = targetBattery.CurrentCharge;
        var required = battery.MaxCharge - _predictedBattery.GetCharge((comp.BatteryUid.Value, battery));
        // higher tier storages can charge more
        var maxDrained = pnb.MaxSupply * comp.DrainTime;
        var input = Math.Min(Math.Min(available, required / comp.DrainEfficiency), maxDrained);
        if (!_battery.TryUseCharge((target, targetBattery), input))
            return false;

        var output = input * comp.DrainEfficiency;
        // PowerCells use PredictedBatteryComponent
        // SMES, substations and APCs use BatteryComponent
        _predictedBattery.ChangeCharge((comp.BatteryUid.Value, battery), output);
        // TODO: create effect message or something
        Spawn("EffectSparks", Transform(target).Coordinates);
        _audio.PlayPvs(comp.SparkSound, target);
        _popup.PopupEntity(Loc.GetString("battery-drainer-success", ("battery", target)), uid, uid);

        // Harmony start
        var ev = new OnBatteryDrained();
        RaiseLocalEvent(uid, ref ev);
        // Harmony End

        // repeat the doafter until battery is full
        return !_predictedBattery.IsFull((comp.BatteryUid.Value, battery));
    }

    // Harmony start - adds support for alerts
    private void OnCompInit(Entity<BatteryDrainerComponent> entity, ref ComponentInit args)
    {
        if (entity.Comp.UseAlert)
            _alertsSystem.ShowAlert(entity.Owner, entity.Comp.DrainerAlertProtoId, 1);
    }

    private void OnCompRemoved(Entity<BatteryDrainerComponent> entity, ref ComponentRemove args)
    {
        if (entity.Comp.UseAlert)
            _alertsSystem.ClearAlert(entity.Owner, entity.Comp.DrainerAlertProtoId);
    }

    private void OnToggleDraining(Entity<BatteryDrainerComponent> ent, ref ToggleDrainingEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.Draining = !ent.Comp.Draining;
        _alertsSystem.ShowAlert(ent.Owner, ent.Comp.DrainerAlertProtoId, (short)(ent.Comp.Draining ? 1 : 0));
        //DirtyField(ent.AsNullable(), nameof(ent.Comp.Draining), null);

        args.Handled = true;
    }
    // Harmony End
}

// Harmony Start
/// <summary>
/// Event raised on Battery drained.
/// </summary>
[ByRefEvent]
public record struct OnBatteryDrained;
// Harmony End
