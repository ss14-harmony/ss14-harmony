using Content.Shared._Harmony.Light; // Harmony
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots; // Harmony
using Content.Shared.Damage.Components; // Harmony
using Content.Shared.Database;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Interaction; // Harmony
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Kitchen;
using Content.Shared.Medical; // Harmony
using Content.Shared.Rejuvenate;
using Content.Shared.Throwing; // Harmony
using Content.Shared.Weapons.Melee; // Harmony
using Content.Shared.Weapons.Melee.Components; // Harmony
using Content.Shared.Weapons.Melee.Events; // Harmony
using Content.Shared.Weapons.Ranged.Events; // Harmony
using Robust.Shared.Containers; // Harmony
using Robust.Shared.Timing; // Harmony

namespace Content.Shared.Power.EntitySystems;

/// <summary>
///  Handles sabotaged/rigged objects
/// </summary>
public sealed partial class RiggableSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedExplosionSystem _explosionSystem = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private IGameTiming _timing = default!; // Harmony
    [Dependency] private SharedContainerSystem _container = default!; // Harmony
    [Dependency] private SharedDefibrillatorSystem _defib = default!; // Harmony

    [SubscribeLocalEvent]
    private void OnRejuvenate(Entity<RiggableComponent> entity, ref RejuvenateEvent args)
    {
        if (!_solution.TryGetSolution(entity.Owner, entity.Comp.Solution, out var solution, true))
            return;

        _solution.RemoveAllSolution(solution.Value);
        entity.Comp.IsRigged = false;

        if (_container.TryGetContainingContainer(entity.Owner, out var container)) // Harmony
            RemComp<RiggedItemComponent>(container.Owner); // Harmony

        DirtyField(entity, entity.Comp, nameof(RiggableComponent.IsRigged));
    }

    [SubscribeLocalEvent]
    private void OnMicrowaved(Entity<RiggableComponent> entity, ref BeingMicrowavedEvent args)
    {
        if (!entity.Comp.IsRigged)
            return;

        var charge = _battery.GetCharge(entity.Owner);
        Explode(entity, charge, args.User);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnSolutionChanged(Entity<RiggableComponent> entity, ref SolutionChangedEvent args)
    {
        if (args.Solution.Comp.Id != entity.Comp.Solution)
            return;

        var wasRigged = entity.Comp.IsRigged;
        var solution = args.Solution.Comp.Solution;
        var quantity = solution.GetReagentQuantity(entity.Comp.Reagent.Reagent);
        entity.Comp.IsRigged = quantity >= entity.Comp.Reagent.Quantity;

        if (!entity.Comp.IsRigged) // Harmony
            RemComp<RiggedItemComponent>(entity.Owner);

        if (wasRigged || !entity.Comp.IsRigged)
            return;

        _adminLogger.Add(LogType.Explosion, LogImpact.Medium, $"{ToPrettyString(entity)} has been rigged up to explode when used.");

        // Harmony / Commented out, injection itself shouldn't cause a battery to explode

        // if (!TryComp<ItemToggleComponent>(entity, out var toggleComp) || !toggleComp.Activated)
            // return;

        // Explode(entity, _battery.GetCharge(entity.Owner));
    }

    [SubscribeLocalEvent]
    private void OnChargeChanged(Entity<RiggableComponent> entity, ref ChargeChangedEvent args)
    {
        if (!entity.Comp.IsRigged)
            return;

        if (args.CurrentCharge == 0f)
            return; // No charge to cause an explosion.

        // Don't explode if we are not using any charge.
        if (args.CurrentChargeRate == 0f && args.Delta == 0f)
            return;

        // Harmony Start

        if (_container.TryGetContainingContainer(entity.Owner, out var container) &&
            HasComp<DefibrillatorComponent>(container.Owner))
            return; // Defibs to blow up when you try to use them, not when turned on

        // Harmony End

        Explode(entity, args.CurrentCharge, entity.Comp.LastUser); // Harmony, LastUser
    }

    [SubscribeLocalEvent]
    private void OnToggled(Entity<RiggableComponent> entity, ref ItemToggledEvent args)
    {
        if (!args.Activated || !entity.Comp.IsRigged)
            return;

        if (HasComp<MeleeWeaponComponent>(entity) || HasComp<DefibrillatorComponent>(entity)) // Harmony
            return;

        entity.Comp.LastUser = args.User;
    }

    // Harmony Start
    [SubscribeLocalEvent]
    private void OnItemToggled(Entity<RiggedItemComponent> entity, ref ItemToggledEvent args)
    {
        if (!args.Activated)
            return;

        if (entity.Comp.Child is not { } child || !TryComp<RiggableComponent>(child, out var riggable))
            return;

        if (!riggable.IsRigged)
            return;

        riggable.LastUser = args.User;
    }

    [SubscribeLocalEvent]
    private void OnInserted(Entity<RiggableComponent> entity, ref EntGotInsertedIntoContainerMessage args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!HasComp<ItemSlotsComponent>(args.Container.Owner))
            return;

        if (entity.Comp.IsRigged)
        {
            var item = EnsureComp<RiggedItemComponent>(args.Container.Owner);
            item.Child = entity;
        }
    }

    [SubscribeLocalEvent]
    private void OnRemoved(Entity<RiggableComponent> entity, ref EntGotRemovedFromContainerMessage args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        RemComp<RiggedItemComponent>(args.Container.Owner);
    }

    [SubscribeLocalEvent]
    private void OnHandheldLightTurnedOn(Entity<RiggableComponent> entity, ref HandheldLightTurnedOnEvent args)
    {
        if (!entity.Comp.IsRigged)
            return;

        entity.Comp.LastUser = args.User;
    }

    [SubscribeLocalEvent]
    private void OnItemHandheldLightTurnedOn(Entity<RiggedItemComponent> entity, ref HandheldLightTurnedOnEvent args)
    {
        if (entity.Comp.Child is not { } child || !TryComp<RiggableComponent>(child, out var riggable))
            return;

        if (!riggable.IsRigged)
            return;

        riggable.LastUser = args.User;
    }

    [SubscribeLocalEvent]
    private void OnAfterInteract(Entity<RiggableComponent> entity, ref AfterInteractEvent args)
    {
        if (!args.CanReach)
            return;

        if (args.Target is not { } target)
            return;

        if (!entity.Comp.IsRigged)
            return;

        entity.Comp.LastUser = args.User;

        if (HasComp<DefibrillatorComponent>(entity))
        {
            if (_defib.CanZap(entity.Owner, target, args.User))
            {
                Explode(entity, _battery.GetCharge(entity.Owner), args.User);
                args.Handled = true; // Just to stop the DoAfter
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnItemAfterInteract(Entity<RiggedItemComponent> entity, ref AfterInteractEvent args)
    {
        if (!args.CanReach)
            return;

        if (args.Target is not { } target)
            return;

        if (entity.Comp.Child is not { } child || !TryComp<RiggableComponent>(child, out var riggable))
            return;

        if (!riggable.IsRigged)
            return;

        riggable.LastUser = args.User;

        if (HasComp<DefibrillatorComponent>(entity))
        {
            if (_defib.CanZap(entity.Owner, target, args.User))
            {
                Explode((child, riggable), _battery.GetCharge(child), args.User);
                args.Handled = true;
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnRangedInteract(Entity<RiggableComponent> entity, ref BeforeRangedInteractEvent args)
    {
        if (!args.CanReach)
            return;

        if (args.Target == null)
            return;

        if (!entity.Comp.IsRigged)
            return;

        entity.Comp.LastUser = args.User;
    }

    [SubscribeLocalEvent]
    private void OnItemRangedInteract(Entity<RiggedItemComponent> entity, ref BeforeRangedInteractEvent args)
    {
        if (!args.CanReach)
            return;

        if (args.Target == null)
            return;

        if (entity.Comp.Child is not { } child || !TryComp<RiggableComponent>(child, out var riggable))
            return;

        if (!riggable.IsRigged)
            return;

        riggable.LastUser = args.User;
    }

    [SubscribeLocalEvent]
    private void OnMeleeHit(Entity<RiggableComponent> entity, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        if (args.HitEntities.Count == 0)
            return;

        if (!entity.Comp.IsRigged)
            return;

        if (TryComp<ItemToggleComponent>(entity, out var toggle) && !toggle.Activated)
            return;

        Explode(entity, _battery.GetCharge(entity.Owner), args.User);
    }

    [SubscribeLocalEvent]
    private void OnItemMeleeHit(Entity<RiggedItemComponent> entity, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        if (args.HitEntities.Count == 0)
            return;

        if (TryComp<ItemToggleComponent>(entity, out var toggle) && !toggle.Activated)
            return;

        if (entity.Comp.Child is not { } child || !TryComp<RiggableComponent>(child, out var riggable))
            return;

        if (!riggable.IsRigged)
            return;

        Explode((child, riggable), _battery.GetCharge(child), args.User);
    }

    [SubscribeLocalEvent]
    private void OnThrown(Entity<RiggableComponent> entity, ref ThrownEvent args)
    {
        if (!HasComp<MeleeThrowOnHitComponent>(entity) && !HasComp<StaminaDamageOnCollideComponent>(entity))
            return;

        if (!entity.Comp.IsRigged)
            return;

        if (TryComp<ItemToggleComponent>(entity, out var toggle) && !toggle.Activated)
            return;

        Explode(entity, _battery.GetCharge(entity.Owner), args.User);
    }

    [SubscribeLocalEvent]
    private void OnItemThrown(Entity<RiggedItemComponent> entity, ref ThrownEvent args)
    {
        if (!HasComp<MeleeThrowOnHitComponent>(entity) && !HasComp<StaminaDamageOnCollideComponent>(entity))
            return;

        if (TryComp<ItemToggleComponent>(entity, out var toggle) && !toggle.Activated)
            return;

        if (entity.Comp.Child is not { } child || !TryComp<RiggableComponent>(child, out var riggable))
            return;

        if (!riggable.IsRigged)
            return;

        Explode((child, riggable), _battery.GetCharge(child), args.User);
    }

    [SubscribeLocalEvent]
    private void OnShotAttempted(Entity<RiggableComponent> entity, ref ShotAttemptedEvent args)
    {
        if (!entity.Comp.IsRigged)
            return;

        entity.Comp.LastUser = args.User;

        Explode(entity, _battery.GetCharge(entity.Owner), args.User);
    }

    [SubscribeLocalEvent]
    private void OnItemShotAttempted(Entity<RiggedItemComponent> entity, ref ShotAttemptedEvent args)
    {
        if (entity.Comp.Child is not { } child || !TryComp<RiggableComponent>(child, out var riggable))
            return;

        if (!riggable.IsRigged)
            return;

        args.Cancel();

        Explode((child, riggable), _battery.GetCharge(child), args.User);
    }

    // Harmony End

    public void Explode(Entity<RiggableComponent> entity, float charge, EntityUid? cause = null)
    {
        if (entity.Comp.Exploded || charge == 0f)
            return;

        var radius = MathF.Min(5, MathF.Sqrt(charge) / 9);

        // Explosion system also queues entity deletion
        _explosionSystem.TriggerExplosive(entity, radius: radius, user: cause, ignoreCauseRes: cause); // Harmony / ignoreCauseRes so that the user takes AP damage

        entity.Comp.Exploded = true;
        DirtyField(entity, entity.Comp, nameof(RiggableComponent.Exploded));
    }
}
