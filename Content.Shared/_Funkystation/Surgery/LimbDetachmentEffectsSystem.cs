using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Handles effects when limbs are detached: hide sprite layers, add movement modifiers.
/// Arms: hand removal is handled by HandOrganSystem via OrganGotRemovedEvent propagation.
/// Legs: one leg = slow, both legs = crawling.
/// </summary>
public sealed class LimbDetachmentEffectsSystem : EntitySystem
{
    private static readonly string[] LimbCategories = ["ArmLeft", "ArmRight", "LegLeft", "LegRight"];
    private static readonly string[] HandCategories = ["HandLeft", "HandRight"];
    private static readonly string[] FootCategories = ["FootLeft", "FootRight"];

    private static readonly IReadOnlyDictionary<string, HumanoidVisualLayers[]> CategoryToLayers = new Dictionary<string, HumanoidVisualLayers[]>
    {
        ["ArmLeft"] = [HumanoidVisualLayers.LArm, HumanoidVisualLayers.LHand],
        ["ArmRight"] = [HumanoidVisualLayers.RArm, HumanoidVisualLayers.RHand],
        ["LegLeft"] = [HumanoidVisualLayers.LLeg, HumanoidVisualLayers.LFoot],
        ["LegRight"] = [HumanoidVisualLayers.RLeg, HumanoidVisualLayers.RFoot],
        ["HandLeft"] = [HumanoidVisualLayers.LHand],
        ["HandRight"] = [HumanoidVisualLayers.RHand],
        ["FootLeft"] = [HumanoidVisualLayers.LFoot],
        ["FootRight"] = [HumanoidVisualLayers.RFoot],
    };

    [Dependency] private BodySystem _body = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrganComponent, EntGotRemovedFromContainerMessage>(OnOrganRemovedFromBody);
        SubscribeLocalEvent<OrganComponent, EntGotInsertedIntoContainerMessage>(OnOrganInsertedIntoBody);
        SubscribeLocalEvent<MissingLimbMovementModifierComponent, RefreshMovementSpeedModifiersEvent>(OnMissingLimbRefreshSpeed);
        SubscribeLocalEvent<LegsMissingComponent, ComponentStartup>(OnLegsMissingStartup);
        SubscribeLocalEvent<LegsMissingComponent, ComponentShutdown>(OnLegsMissingShutdown);
        SubscribeLocalEvent<LegsMissingComponent, StandUpAttemptEvent>(OnLegsMissingStandUpAttempt);
        SubscribeLocalEvent<FeetMissingComponent, ComponentStartup>(OnFeetMissingStartup);
        SubscribeLocalEvent<FeetMissingComponent, ComponentShutdown>(OnFeetMissingShutdown);
        SubscribeLocalEvent<FeetMissingComponent, StandUpAttemptEvent>(OnFeetMissingStandUpAttempt);
    }

    /// <summary>
    /// Recompute foot-derived movement after trait stamp/unstamp or other non-container paths.
    /// </summary>
    public void RefreshFootStateForBody(EntityUid body)
    {
        UpdateFeetMovement(body);
    }

    private void OnOrganRemovedFromBody(Entity<OrganComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        if (!_body.TryGetRootBodyFromOrganContainer(args.Container, out var body))
            return;

        if (!Exists(body) || TerminatingOrDeleted(body))
            return;

        var organ = ent.Comp;
        if (organ.Category is not { } category)
            return;

        var categoryStr = category.ToString();
        if (!CategoryToLayers.TryGetValue(categoryStr, out var layers))
            return;

        if (!LimbCategories.Contains(categoryStr) && !HandCategories.Contains(categoryStr) && !FootCategories.Contains(categoryStr))
            return;

        // Limb visibility is handled by VisualBodySystem when organ is removed (sets layer to Invalid).

        if (TryComp<AppearanceComponent>(body, out var appearance))
        {
            foreach (var layer in layers)
            {
                _appearance.SetData(body, layer, DamageOverlayLayerState.AllDisabled, appearance);
            }
        }

        if (categoryStr is "LegLeft" or "LegRight")
        {
            UpdateLegMovement(body);
        }

        if (categoryStr is "FootLeft" or "FootRight")
        {
            UpdateFeetMovement(body);
        }
    }

    private void OnOrganInsertedIntoBody(Entity<OrganComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        if (!_body.TryGetRootBodyFromOrganContainer(args.Container, out var body))
            return;

        if (!Exists(body) || TerminatingOrDeleted(body))
            return;

        var organ = ent.Comp;
        if (organ.Category is not { } category)
            return;

        var categoryStr = category.ToString();
        if (!CategoryToLayers.TryGetValue(categoryStr, out var layers))
            return;

        if (!LimbCategories.Contains(categoryStr) && !HandCategories.Contains(categoryStr) && !FootCategories.Contains(categoryStr))
            return;

        // Limb visibility is handled by VisualBodySystem when organ is inserted (applies organ's VisualOrganComponent.Data).

        // Cyber limbs set BloodDisabled via CyberLimbAppearanceSystem - don't overwrite with AllEnabled
        if (TryComp<AppearanceComponent>(body, out var appearance) && !HasComp<CyberLimbComponent>(ent))
        {
            foreach (var layer in layers)
            {
                _appearance.SetData(body, layer, DamageOverlayLayerState.AllEnabled, appearance);
            }
        }

        if (categoryStr is "LegLeft" or "LegRight")
        {
            UpdateLegMovement(body);
        }

        if (categoryStr is "FootLeft" or "FootRight")
        {
            UpdateFeetMovement(body);
        }
    }

    private void OnMissingLimbRefreshSpeed(Entity<MissingLimbMovementModifierComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.WalkSpeedModifier, ent.Comp.SprintSpeedModifier);
    }

    private void OnLegsMissingStartup(Entity<LegsMissingComponent> ent, ref ComponentStartup args)
    {
        if (LifeStage(ent.Owner) >= EntityLifeStage.Terminating)
            return;

        _stun.TryCrawling((ent.Owner, (CrawlerComponent?)null), null, refresh: true, autoStand: false, drop: false, force: true);
        // Knockdown() only sets AutoStand on first KnockedDown add; if already knocked down, enforce no auto-stand.
        _stun.SetAutoStand((ent.Owner, (KnockedDownComponent?)null));
    }

    private void OnLegsMissingStandUpAttempt(Entity<LegsMissingComponent> ent, ref StandUpAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = true;
        args.Message = (Loc.GetString("legs-missing-stand-attempt"), PopupType.SmallCaution);
        args.Autostand = false;
    }

    private void OnLegsMissingShutdown(Entity<LegsMissingComponent> ent, ref ComponentShutdown args)
    {
        if (LifeStage(ent.Owner) >= EntityLifeStage.Terminating)
            return;

        _stun.CancelKnockdownDoAfter((ent.Owner, (KnockedDownComponent?)null));
        _stun.ForceStandUp((ent.Owner, (KnockedDownComponent?)null));
        // ForceStandUp can return early (empty hands, stamina, tight collision). Still clear leg-forced knockdown.
        if (HasComp<KnockedDownComponent>(ent.Owner))
            RemComp<KnockedDownComponent>(ent.Owner);
    }

    private void OnFeetMissingStartup(Entity<FeetMissingComponent> ent, ref ComponentStartup args)
    {
        if (LifeStage(ent.Owner) >= EntityLifeStage.Terminating)
            return;

        _stun.TryCrawling((ent.Owner, (CrawlerComponent?)null), null, refresh: true, autoStand: false, drop: false, force: true);
        _stun.SetAutoStand((ent.Owner, (KnockedDownComponent?)null));
    }

    private void OnFeetMissingStandUpAttempt(Entity<FeetMissingComponent> ent, ref StandUpAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = true;
        args.Message = (Loc.GetString("feet-missing-stand-attempt"), PopupType.SmallCaution);
        args.Autostand = false;
    }

    private void OnFeetMissingShutdown(Entity<FeetMissingComponent> ent, ref ComponentShutdown args)
    {
        if (LifeStage(ent.Owner) >= EntityLifeStage.Terminating)
            return;

        _stun.CancelKnockdownDoAfter((ent.Owner, (KnockedDownComponent?)null));
        _stun.ForceStandUp((ent.Owner, (KnockedDownComponent?)null));
        if (HasComp<KnockedDownComponent>(ent.Owner))
            RemComp<KnockedDownComponent>(ent.Owner);
    }

    private int CountLegs(EntityUid body)
    {
        var n = 0;
        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (TryComp<OrganComponent>(organ, out var oComp) && oComp.Category is { } cat)
            {
                var c = cat.ToString();
                if (c is "LegLeft" or "LegRight")
                    n++;
            }
        }

        return n;
    }

    /// <summary>
    /// Counts sides that can bear weight: non-paraplegic foot organs, or a cyber leg on that side (integrated prosthetic foot).
    /// Replacing a leg removes the separate foot organ; cyber legs still provide mobility for that side.
    /// </summary>
    private int CountEffectiveMobilityFeet(EntityUid body)
    {
        var hasLeftFoot = false;
        var hasRightFoot = false;
        var leftCyberLeg = false;
        var rightCyberLeg = false;

        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (!TryComp<OrganComponent>(organ, out var oComp) || oComp.Category is not { } cat)
                continue;

            var c = cat.ToString();
            switch (c)
            {
                case "FootLeft":
                    if (!HasComp<FootTraitParaplegicComponent>(organ))
                        hasLeftFoot = true;
                    break;
                case "FootRight":
                    if (!HasComp<FootTraitParaplegicComponent>(organ))
                        hasRightFoot = true;
                    break;
                case "LegLeft":
                    if (HasComp<CyberLimbComponent>(organ))
                        leftCyberLeg = true;
                    break;
                case "LegRight":
                    if (HasComp<CyberLimbComponent>(organ))
                        rightCyberLeg = true;
                    break;
            }
        }

        var n = 0;
        if (hasLeftFoot || leftCyberLeg)
            n++;
        if (hasRightFoot || rightCyberLeg)
            n++;
        return n;
    }

    private void UpdateFeetMovement(EntityUid body)
    {
        if (!Exists(body) || TerminatingOrDeleted(body))
            return;

        var legCount = CountLegs(body);
        var healthyFeet = CountEffectiveMobilityFeet(body);

        // No legs: leg-loss path owns crawl / modifiers; don't stack FeetMissing.
        if (legCount == 0)
        {
            RemComp<FeetMissingComponent>(body);
        }
        else if (healthyFeet == 0)
        {
            EnsureComp<FeetMissingComponent>(body);
            RemComp<MissingLimbMovementModifierComponent>(body);
        }
        else if (healthyFeet == 1)
        {
            RemComp<FeetMissingComponent>(body);
            var mod = EnsureComp<MissingLimbMovementModifierComponent>(body);
            if (legCount == 1)
            {
                mod.WalkSpeedModifier = 0.6f * 0.85f;
                mod.SprintSpeedModifier = 0.6f * 0.85f;
            }
            else
            {
                mod.WalkSpeedModifier = 0.85f;
                mod.SprintSpeedModifier = 0.85f;
            }

            Dirty(body, mod);
        }
        else
        {
            RemComp<FeetMissingComponent>(body);
            if (legCount >= 2)
            {
                RemComp<MissingLimbMovementModifierComponent>(body);
            }
            else
            {
                var mod = EnsureComp<MissingLimbMovementModifierComponent>(body);
                mod.WalkSpeedModifier = 0.6f;
                mod.SprintSpeedModifier = 0.6f;
                Dirty(body, mod);
            }
        }

        _movementSpeed.RefreshMovementSpeedModifiers(body);
    }

    private void UpdateLegMovement(EntityUid body)
    {
        if (!Exists(body) || TerminatingOrDeleted(body))
            return;

        var legCount = 0;
        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (TryComp<OrganComponent>(organ, out var oComp) && oComp.Category is { } cat)
            {
                var c = cat.ToString();
                if (c is "LegLeft" or "LegRight")
                    legCount++;
            }
        }

        if (legCount >= 2)
        {
            RemComp<LegsMissingComponent>(body);
            RemComp<MissingLimbMovementModifierComponent>(body);
            // OnLegsMissingShutdown will call ForceStandUp when LegsMissingComponent is removed
        }
        else if (legCount == 1)
        {
            RemComp<LegsMissingComponent>(body);
            var mod = EnsureComp<MissingLimbMovementModifierComponent>(body);
            mod.WalkSpeedModifier = 0.6f;
            mod.SprintSpeedModifier = 0.6f;
            Dirty(body, mod);
        }
        else
        {
            RemComp<MissingLimbMovementModifierComponent>(body);
            EnsureComp<LegsMissingComponent>(body);
        }

        UpdateFeetMovement(body);
    }
}
