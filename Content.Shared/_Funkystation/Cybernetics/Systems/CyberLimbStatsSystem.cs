using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.Emp;
using Content.Shared.Movement.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Storage;
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

    private const float UpdateInterval = 1f;
    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, CyberLimbAttachedToBodyEvent>(OnCyberLimbAttached);
        SubscribeLocalEvent<BodyComponent, CyberLimbDetachedFromBodyEvent>(OnCyberLimbDetached);
        SubscribeLocalEvent<BodyComponent, CyberMaintenanceStateChangedEvent>(OnMaintenanceStateChanged);
        SubscribeLocalEvent<BodyComponent, CyberLimbStatsRecalcEvent>(OnStatsRecalc);
        SubscribeLocalEvent<CyberLimbStatsComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<CyberLimbStatsComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(UpdateInterval);
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

        var (_, cpuCount, _) = _moduleSystem.GetModuleCounts(body);
        stats.Efficiency = _moduleSystem.GetLimbEfficiencyFromCpus(cpuCount);

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
        args.ModifySpeed(ent.Comp.Efficiency);
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

        var (_, cpuCount, _) = _moduleSystem.GetModuleCounts(body);
        var limbEfficiency = _moduleSystem.GetLimbEfficiencyFromCpus(cpuCount);
        var depleted = (stats.ServiceTimeRemaining <= TimeSpan.Zero) || (stats.BatteryMax > 0 && stats.BatteryRemaining <= 0);
        var newEfficiency = limbEfficiency * (depleted ? 0.5f : 1f);
        stats.Efficiency = newEfficiency;

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
            var (_, cpuCount, _) = _moduleSystem.GetModuleCounts(body);
            var limbEfficiency = _moduleSystem.GetLimbEfficiencyFromCpus(cpuCount);
            var newEfficiency = limbEfficiency * (depleted ? 0.5f : 1f);
            if (stats.Efficiency != newEfficiency)
            {
                stats.Efficiency = newEfficiency;
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

            var limbEfficiency = _moduleSystem.GetLimbEfficiencyFromCpus(cpuCount);
            var depleted = (stats.ServiceTimeRemaining <= TimeSpan.Zero) || (stats.BatteryMax > 0 && stats.BatteryRemaining <= 0);
            var newEfficiency = limbEfficiency * (depleted ? 0.5f : 1f);

            if (stats.Efficiency != newEfficiency)
            {
                stats.Efficiency = newEfficiency;
                _movementSpeed.RefreshMovementSpeedModifiers(uid);
            }

            Dirty(uid, stats);
        }
    }
}
