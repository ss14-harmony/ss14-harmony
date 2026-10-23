using Content.Server.Chat.Systems;
using Content.Server.Lightning;
// using Content.Server.Popups; // Box Change, using Content.Shared.Popup instead
using Content.Shared.PowerCell;
using Content.Server._EE.Silicon.Charge;
using Content.Shared._EE.Silicon.DeadStartupButton;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Electrocution;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Content.Shared.Damage.Components;
// Start of Box Change to make IPCs work with Unrevivable trait
using Content.Shared.Traits.Assorted;
using Content.Shared.Popups;
// End of Box Change

namespace Content.Server._EE.Silicon.DeadStartupButton;

public sealed class DeadStartupButtonSystem : SharedDeadStartupButtonSystem
{ 
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    // Start of Box Change, using SharedPopupSystem instead
    //[Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    // End of Box Change
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly SiliconChargeSystem _siliconChargeSystem = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeadStartupButtonComponent, OnDoAfterButtonPressedEvent>(OnDoAfter);
        SubscribeLocalEvent<DeadStartupButtonComponent, ElectrocutedEvent>(OnElectrocuted);
        SubscribeLocalEvent<DeadStartupButtonComponent, MobStateChangedEvent>(OnMobStateChanged);

    }

    private void OnDoAfter(EntityUid uid, DeadStartupButtonComponent comp, OnDoAfterButtonPressedEvent args)
    {
        if (args.Handled || args.Cancelled
            || !TryComp<MobStateComponent>(uid, out var mobStateComponent)
            || !_mobState.IsDead(uid, mobStateComponent)
            || !TryComp<MobThresholdsComponent>(uid, out var mobThresholdsComponent)
            || !TryComp<DamageableComponent>(uid, out var damageable)
            || !_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var criticalThreshold, mobThresholdsComponent))
            return;

        // Start of Box Change to make IPCs work with Unrevivable trait
        if (TryComp<UnrevivableComponent>(uid, out var unrevivableComponent))
        {
            _audio.PlayPvs(comp.BuzzSound, uid, AudioHelpers.WithVariation(0.05f, _robustRandom));
            _popup.PopupEntity(Loc.GetString("dead-startup-system-reboot-unrevivable", ("target", MetaData(uid).EntityName)), uid, PopupType.MediumCaution);
            Spawn("EffectSparks", Transform(uid).Coordinates);
        }
        else if (damageable.TotalDamage < criticalThreshold)
        // End of Box Change
            _mobState.ChangeMobState(uid, MobState.Alive, mobStateComponent);
        else
        {
            _audio.PlayPvs(comp.BuzzSound, uid, AudioHelpers.WithVariation(0.05f, _robustRandom));
            _popup.PopupEntity(Loc.GetString("dead-startup-system-reboot-failed", ("target", MetaData(uid).EntityName)), uid);
            Spawn("EffectSparks", Transform(uid).Coordinates);
        }
    }

    private void OnElectrocuted(EntityUid uid, DeadStartupButtonComponent comp, ElectrocutedEvent args)
    {
        if (!TryComp<MobStateComponent>(uid, out var mobStateComponent)
            || !_mobState.IsDead(uid, mobStateComponent)
            || !_siliconChargeSystem.TryGetSiliconBattery(uid, out var bateria)
            || bateria.Value.Comp.LastCharge <= 0)
            return;

        _lightning.ShootRandomLightnings(uid, 2, 4);
        _powerCell.TryUseCharge(uid, bateria.Value.Comp.LastCharge);

    }

    private void OnMobStateChanged(EntityUid uid, DeadStartupButtonComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            return;

        _popup.PopupEntity(Loc.GetString("dead-startup-system-reboot-success", ("target", MetaData(uid).EntityName)), uid);
        _audio.PlayPvs(comp.Sound, uid);
    }
}
