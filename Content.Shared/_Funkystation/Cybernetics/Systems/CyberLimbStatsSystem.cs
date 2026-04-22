using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Cybernetics.Systems;

public sealed class CyberLimbStatsSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly CyberLimbDamageProtectionSystem _damageProtection = default!;
    [Dependency] private readonly CyberLimbModuleSystem _moduleSystem = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const float UpdateInterval = 1f;
    private TimeSpan _nextUpdate = TimeSpan.Zero;

    private const string ArmLeft = "ArmLeft";
    private const string ArmRight = "ArmRight";
    private const string LegLeft = "LegLeft";
    private const string LegRight = "LegRight";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, CyberLimbAttachedToBodyEvent>(OnCyberLimbAttached);
        SubscribeLocalEvent<BodyComponent, CyberLimbDetachedFromBodyEvent>(OnCyberLimbDetached);
        SubscribeLocalEvent<BodyComponent, CyberMaintenanceStateChangedEvent>(OnMaintenanceStateChanged);
        SubscribeLocalEvent<BodyComponent, CyberLimbStatsRecalcEvent>(OnStatsRecalc);
        SubscribeLocalEvent<CyberLimbStatsComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<CyberLimbStatsComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<CyberLimbStatsComponent, DoAfterDelayMultiplierEvent>(OnDoAfterDelayMultiplier);

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(UpdateInterval);
    }

    /// <summary>
    /// Returns true if the body has at least one cyber limb in either leg slot.
    /// Used to gate leg-based penalties (movement) so a body with only cyber arms isn't slowed.
    /// </summary>
    public bool HasCyberLegs(EntityUid body)
    {
        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (!HasComp<CyberLimbComponent>(organ))
                continue;
            if (!TryComp<OrganComponent>(organ, out var organComp) || organComp.Category is not { } category)
                continue;
            if (category.Id == LegLeft || category.Id == LegRight)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if the body has at least one cyber limb in either arm slot.
    /// Used to gate arm-based penalties (interaction / do-after speed) so a body with only cyber legs
    /// isn't slowed when manipulating things.
    /// </summary>
    public bool HasCyberArms(EntityUid body)
    {
        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (!HasComp<CyberLimbComponent>(organ))
                continue;
            if (!TryComp<OrganComponent>(organ, out var organComp) || organComp.Category is not { } category)
                continue;
            if (category.Id == ArmLeft || category.Id == ArmRight)
                return true;
        }

        return false;
    }

    private void OnCyberLimbAttached(Entity<BodyComponent> ent, ref CyberLimbAttachedToBodyEvent args)
    {
        if (_timing.ApplyingState)
            return;

        var body = args.Body;
        var limb = args.Limb;

        if (TryComp<CyberLimbStatsComponent>(body, out var existingStats))
        {
            existingStats.BaseServiceRemaining += existingStats.BaseServiceTimePerLimb;
            FillMatterBinsInLimb(limb);
        }
        else
        {
            var stats = EnsureComp<CyberLimbStatsComponent>(body);
            stats.BaseServiceRemaining = stats.BaseServiceTimePerLimb;
            FillMatterBinsInLimb(limb);
        }

        if (HasComp<MilitaryCyberLimbComponent>(limb))
            _damageProtection.OnMilitaryLimbAttached(body);

        RecomputeAndRefresh(body);
    }

    private void OnCyberLimbDetached(Entity<BodyComponent> ent, ref CyberLimbDetachedFromBodyEvent args)
    {
        if (_timing.ApplyingState)
            return;

        var body = args.Body;
        var cyberCount = _body.GetAllOrgans(body).Count(o => HasComp<CyberLimbComponent>(o));

        if (HasComp<MilitaryCyberLimbComponent>(args.Limb))
            _damageProtection.OnMilitaryLimbDetached(body);

        if (cyberCount == 0)
        {
            RemComp<CyberLimbStatsComponent>(body);
            _movementSpeed.RefreshMovementSpeedModifiers(body);
            return;
        }

        if (TryComp<CyberLimbStatsComponent>(body, out var stats))
        {
            stats.BaseServiceRemaining = stats.BaseServiceTimePerLimb * cyberCount;
            RecomputeAndRefresh(body);
        }
    }

    private void OnMaintenanceStateChanged(Entity<BodyComponent> ent, ref CyberMaintenanceStateChangedEvent args)
    {
        var body = ent.Owner;

        if (!args.RepairCompleted || !TryComp<CyberLimbStatsComponent>(body, out var stats))
            return;

        var cyberCount = _body.GetAllOrgans(body).Count(o => HasComp<CyberLimbComponent>(o));
        stats.BaseServiceRemaining = stats.BaseServiceTimePerLimb * cyberCount;

        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (!HasComp<CyberLimbComponent>(organ))
                continue;
            FillMatterBinsInLimb(organ);
        }

        var (armCpuCount, legCpuCount) = _moduleSystem.GetArmLegCpuCounts(body);
        stats.ArmEfficiency = _moduleSystem.GetArmEfficiencyFromCpus(armCpuCount);
        stats.LegEfficiency = _moduleSystem.GetLegEfficiencyFromCpus(legCpuCount);

        RecomputeAndRefresh(body);
    }

    private void OnStatsRecalc(Entity<BodyComponent> ent, ref CyberLimbStatsRecalcEvent args)
    {
        if (args.Body != ent.Owner)
            return;

        if (!HasComp<CyberLimbStatsComponent>(ent.Owner))
            return;

        RecomputeAndRefresh(ent.Owner);
    }

    private void OnEmpPulse(Entity<CyberLimbStatsComponent> ent, ref EmpPulseEvent args)
    {
        TryUseBatteryCharge(ent.Owner, args.EnergyConsumption);
        args.Affected = true;
    }

    private void OnRefreshMovementSpeed(Entity<CyberLimbStatsComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // Movement speed is driven by the legs, so don't modify it when the body has no cyber legs.
        // A body with only cyber arms should walk at normal speed regardless of maintenance/power state.
        if (!HasCyberLegs(ent.Owner))
            return;

        args.ModifySpeed(ent.Comp.LegEfficiency);
    }

    private void OnDoAfterDelayMultiplier(Entity<CyberLimbStatsComponent> ent, ref DoAfterDelayMultiplierEvent args)
    {
        // Interaction speed is driven by the arms, so skip when the body has no cyber arms.
        // A body with only cyber legs should interact at normal speed regardless of maintenance/power state.
        if (!HasCyberArms(ent.Owner))
            return;

        // Mirror the movement modifier: delay scales by 1/ArmEfficiency. When depleted (ArmEfficiency 0.5 with no CPUs)
        // this doubles the do-after delay - a 50% interaction slowdown symmetric with the 50% movement slowdown.
        var efficiency = ent.Comp.ArmEfficiency;
        if (efficiency <= 0f)
            return;

        args.Multiplier *= 1f / efficiency;
    }

    private void FillMatterBinsInLimb(EntityUid limb)
    {
        if (!TryComp<StorageComponent>(limb, out var storage) || storage.Container == null)
            return;

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (TryComp<CyberLimbMatterBinComponent>(item, out var matterBin))
            {
                var count = TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;
                matterBin.ServiceRemaining = TimeSpan.FromTicks(matterBin.ServiceTime.Ticks * count);
                Dirty(item, matterBin);
            }
        }
    }

    public void RecomputeAndRefresh(EntityUid body)
    {
        if (!TryComp<CyberLimbStatsComponent>(body, out var stats))
            return;

        stats.ServiceTimeMax = _moduleSystem.GetTotalServiceMax(body);
        var totalRemaining = _moduleSystem.GetTotalServiceRemaining(body);
        stats.ServiceTimeRemaining = TimeSpan.FromTicks(Math.Min(totalRemaining.Ticks, stats.ServiceTimeMax.Ticks));

        var batteries = _moduleSystem.GetBatteryEntities(body);
        stats.BatteryRemaining = 0f;
        stats.BatteryMax = 0f;
        foreach (var battery in batteries)
        {
            if (TryComp<BatteryComponent>(battery, out var batteryComp))
            {
                stats.BatteryRemaining += _battery.GetCharge(battery);
                stats.BatteryMax += batteryComp.MaxCharge;
            }
        }

        var (armCpuCount, legCpuCount) = _moduleSystem.GetArmLegCpuCounts(body);
        var depleted = (stats.ServiceTimeRemaining <= TimeSpan.Zero) || (stats.BatteryMax > 0 && stats.BatteryRemaining <= 0);
        var depletionMultiplier = depleted ? 0.5f : 1f;
        stats.ArmEfficiency = _moduleSystem.GetArmEfficiencyFromCpus(armCpuCount) * depletionMultiplier;
        stats.LegEfficiency = _moduleSystem.GetLegEfficiencyFromCpus(legCpuCount) * depletionMultiplier;

        Dirty(body, stats);
        _movementSpeed.RefreshMovementSpeedModifiers(body);
    }

    /// <summary>
    /// Attempts to drain the given amount of charge (joules) from the body's shared battery pool.
    /// Used by components that consume power (e.g. CyberLimbPowerDrawComponent).
    /// </summary>
    /// <returns>The amount actually drained. May be less than requested if batteries are depleted.</returns>
    public float TryUseBatteryCharge(EntityUid body, float amount)
    {
        if (amount <= 0f)
            return 0f;

        var batteries = _moduleSystem.GetBatteryEntities(body);
        var totalDrained = 0f;
        var remaining = amount;

        foreach (var battery in batteries)
        {
            if (remaining <= 0f)
                break;

            var currentCharge = _battery.GetCharge(battery);
            var toDrain = Math.Min(remaining, currentCharge);
            if (toDrain <= 0f)
                continue;

            _battery.SetCharge(battery, currentCharge - toDrain);
            totalDrained += toDrain;
            remaining -= toDrain;
        }

        if (totalDrained > 0f && TryComp<CyberLimbStatsComponent>(body, out var stats))
        {
            stats.BatteryRemaining = 0f;
            foreach (var battery in batteries)
            {
                stats.BatteryRemaining += _battery.GetCharge(battery);
            }
            Dirty(body, stats);
            var depleted = (stats.ServiceTimeRemaining <= TimeSpan.Zero) || (stats.BatteryMax > 0 && stats.BatteryRemaining <= 0);
            var (armCpuCount, legCpuCount) = _moduleSystem.GetArmLegCpuCounts(body);
            var depletionMultiplier = depleted ? 0.5f : 1f;
            var newArmEfficiency = _moduleSystem.GetArmEfficiencyFromCpus(armCpuCount) * depletionMultiplier;
            var newLegEfficiency = _moduleSystem.GetLegEfficiencyFromCpus(legCpuCount) * depletionMultiplier;
            if (stats.ArmEfficiency != newArmEfficiency || stats.LegEfficiency != newLegEfficiency)
            {
                stats.ArmEfficiency = newArmEfficiency;
                stats.LegEfficiency = newLegEfficiency;
                _movementSpeed.RefreshMovementSpeedModifiers(body);
            }
        }

        return totalDrained;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(UpdateInterval);

        var query = EntityQueryEnumerator<CyberLimbStatsComponent>();
        while (query.MoveNext(out var uid, out var stats))
        {
            var drainRemaining = TimeSpan.FromSeconds(1);

            if (stats.BaseServiceRemaining >= drainRemaining)
            {
                stats.BaseServiceRemaining -= drainRemaining;
                drainRemaining = TimeSpan.Zero;
            }
            else
            {
                drainRemaining -= stats.BaseServiceRemaining;
                stats.BaseServiceRemaining = TimeSpan.Zero;
            }

            if (drainRemaining > TimeSpan.Zero)
            {
                var (matterBins, _, _) = _moduleSystem.GetModuleCounts(uid);
                foreach (var mb in matterBins)
                {
                    if (drainRemaining <= TimeSpan.Zero)
                        break;

                    var comp = Comp<CyberLimbMatterBinComponent>(mb);
                    if (comp.ServiceRemaining >= drainRemaining)
                    {
                        comp.ServiceRemaining -= drainRemaining;
                        drainRemaining = TimeSpan.Zero;
                    }
                    else
                    {
                        drainRemaining -= comp.ServiceRemaining;
                        comp.ServiceRemaining = TimeSpan.Zero;
                    }
                    Dirty(mb, comp);
                }
            }

            stats.ServiceTimeRemaining = _moduleSystem.GetTotalServiceRemaining(uid);
            if (stats.ServiceTimeRemaining < TimeSpan.Zero)
                stats.ServiceTimeRemaining = TimeSpan.Zero;

            var (_, cpuCount, capacitorCount) = _moduleSystem.GetModuleCounts(uid);
            var batteries = _moduleSystem.GetBatteryEntities(uid);
            if (batteries.Count > 0)
            {
                var cpuMultiplier = _moduleSystem.GetCpuPowerDrawMultiplier(cpuCount);
                var capacitorMultiplier = _moduleSystem.GetCapacitorBatteryDrainMultiplier(capacitorCount);
                var joulesToDrain = stats.BaseBatteryDrainPerSecond * cpuMultiplier * capacitorMultiplier;
                var remaining = joulesToDrain;

                foreach (var battery in batteries)
                {
                    if (remaining <= 0f)
                        break;

                    var currentCharge = _battery.GetCharge(battery);
                    var toDrain = Math.Min(remaining, currentCharge);
                    if (toDrain <= 0f)
                        continue;

                    _battery.SetCharge(battery, currentCharge - toDrain);
                    remaining -= toDrain;
                }

                stats.BatteryRemaining = 0f;
                stats.BatteryMax = 0f;
                foreach (var battery in batteries)
                {
                    if (TryComp<BatteryComponent>(battery, out var batteryComp))
                    {
                        stats.BatteryRemaining += _battery.GetCharge(battery);
                        stats.BatteryMax += batteryComp.MaxCharge;
                    }
                }
            }

            var (armCpuCount, legCpuCount) = _moduleSystem.GetArmLegCpuCounts(uid);
            var depleted = (stats.ServiceTimeRemaining <= TimeSpan.Zero) || (stats.BatteryMax > 0 && stats.BatteryRemaining <= 0);
            var depletionMultiplier = depleted ? 0.5f : 1f;
            var newArmEfficiency = _moduleSystem.GetArmEfficiencyFromCpus(armCpuCount) * depletionMultiplier;
            var newLegEfficiency = _moduleSystem.GetLegEfficiencyFromCpus(legCpuCount) * depletionMultiplier;

            if (stats.ArmEfficiency != newArmEfficiency || stats.LegEfficiency != newLegEfficiency)
            {
                stats.ArmEfficiency = newArmEfficiency;
                stats.LegEfficiency = newLegEfficiency;
                _movementSpeed.RefreshMovementSpeedModifiers(uid);
            }

            MaybePopupLowService(uid, stats);
            MaybePopupLowPower(uid, stats);

            Dirty(uid, stats);
        }
    }

    private void MaybePopupLowService(EntityUid body, CyberLimbStatsComponent stats)
    {
        if (stats.ServiceTimeMax <= TimeSpan.Zero)
            return;

        var frac = stats.ServiceTimeRemaining.TotalSeconds / stats.ServiceTimeMax.TotalSeconds;
        if (frac >= 0.25)
        {
            if (stats.LowMaintenanceWarned)
            {
                stats.LowMaintenanceWarned = false;
                stats.NextMaintenanceWarning = TimeSpan.Zero;
            }

            return;
        }

        if (stats.LowMaintenanceWarned && _timing.CurTime < stats.NextMaintenanceWarning)
            return;

        _popup.PopupEntity(Loc.GetString("cyber-limb-motors-whirring"), body, body, PopupType.MediumCaution);
        stats.LowMaintenanceWarned = true;
        stats.NextMaintenanceWarning = _timing.CurTime + TimeSpan.FromSeconds(60);
    }

    private void MaybePopupLowPower(EntityUid body, CyberLimbStatsComponent stats)
    {
        if (stats.BatteryMax <= 0f)
            return;

        var percent = 100f * stats.BatteryRemaining / stats.BatteryMax;
        if (percent >= 25f)
        {
            if (stats.LowPowerWarned)
            {
                stats.LowPowerWarned = false;
                stats.NextPowerWarning = TimeSpan.Zero;
            }

            return;
        }

        if (stats.LowPowerWarned && _timing.CurTime < stats.NextPowerWarning)
            return;

        _popup.PopupEntity(Loc.GetString("cyber-limb-low-power"), body, body, PopupType.MediumCaution);
        stats.LowPowerWarned = true;
        stats.NextPowerWarning = _timing.CurTime + TimeSpan.FromSeconds(60);
    }
}
