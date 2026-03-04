using System.Linq;
using Content.Server.Medical.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Integrity.Events;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Temperature.Components;
using Content.Shared.Traits.Assorted;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Server.Body.Systems;
using Content.Server.Medical.LimbRegeneration;

namespace Content.Server.Medical;

public sealed class HealthAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SurgeryLayerSystem _surgeryLayer = default!;
    [Dependency] private readonly LimbRegenerationSystem _limbRegeneration = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HealthAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<HealthAnalyzerComponent, HealthAnalyzerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<HealthAnalyzerComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<HealthAnalyzerComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<HealthAnalyzerComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<SurgeryLayerComponent, SurgeryPenaltyAppliedEvent>(OnSurgeryPenaltyApplied);
        SubscribeLocalEvent<BodyComponent, SurgeryUiRefreshRequestEvent>(OnSurgeryUiRefreshRequest);
        SubscribeLocalEvent<BodyComponent, OrganRemovedFromEvent>(OnBodyOrganRemoved);

        Subs.BuiEvents<HealthAnalyzerComponent>(HealthAnalyzerUiKey.Key, subs =>
        {
            subs.Event<SurgeryRequestBuiMessage>(OnSurgeryRequestBui);
        });
    }

    private void OnSurgeryRequestBui(Entity<HealthAnalyzerComponent> uid, ref SurgeryRequestBuiMessage args)
    {
        if (uid.Comp.ScannedEntity is not { } target)
            return;

        var targetNet = GetNetEntity(target);
        if (args.Target != targetNet)
            return;

        var targetUid = GetEntity(args.Target);
        var bodyPartUid = GetEntity(args.BodyPart);
        var user = args.Actor;

        var ev = new SurgeryRequestEvent(uid.Owner, user, targetUid, bodyPartUid, args.ProcedureId, args.Layer, args.IsImprovised,
            args.Organ.HasValue ? GetEntity(args.Organ.Value) : null);
        RaiseLocalEvent(targetUid, ref ev);

        if (ev.Valid && ev.UsedImprovisedTool && ev.ToolUsed.HasValue && Exists(ev.ToolUsed.Value))
        {
            _popupSystem.PopupEntity(
                Loc.GetString("health-analyzer-surgery-begin-improvised", ("tool", Identity.Name(ev.ToolUsed.Value, EntityManager))),
                user, user, PopupType.Small);
        }
        else if (!ev.Valid && ev.RejectReason != null && Exists(user))
        {
            string msg;
            if (ev.RejectReason == "missing-tool" && _prototypes.TryIndex<SurgeryStepPrototype>(args.ProcedureId.ToString(), out var step) && !string.IsNullOrEmpty(step.RequiredToolTag))
            {
                var proper = Loc.GetString(GetToolTagLocaleKey(step.RequiredToolTag));
                if (step.ImprovisedToolTags.Count > 0)
                {
                    var improvised = string.Join(", ", step.ImprovisedToolTags.Select(t => Loc.GetString(GetToolTagLocaleKey(t))));
                    msg = Loc.GetString("health-analyzer-surgery-error-missing-tool-with-improvised", ("proper", proper), ("improvised", improvised));
                }
                else
                {
                    msg = Loc.GetString("health-analyzer-surgery-error-missing-tool-proper-only", ("proper", proper));
                }
            }
            else
            {
                var msgKey = ev.RejectReason switch
                {
                    "missing-tool" => "health-analyzer-surgery-error-missing-tool",
                    "already-done" => "health-analyzer-surgery-error-already-done",
                    "layer-not-open" => "health-analyzer-surgery-error-layer-not-open",
                    "doafter-failed" => "health-analyzer-surgery-error-doafter-failed",
                    "integrity-over-capacity" => "health-analyzer-integrity-over-capacity",
                    "invalid-entity" => "health-analyzer-surgery-error-invalid-entity",
                    "body-part-not-in-body" => "health-analyzer-surgery-error-body-part-not-in-body",
                    "unknown-step" => "health-analyzer-surgery-error-unknown-step",
                    "layer-mismatch" => "health-analyzer-surgery-error-layer-mismatch",
                    "invalid-limb-type" => "health-analyzer-surgery-error-invalid-limb-type",
                    "unknown-species-or-category" => "health-analyzer-surgery-error-unknown-species-or-category",
                    "invalid-body-part" => "health-analyzer-surgery-error-invalid-body-part",
                    "cannot-detach-limb" => "health-analyzer-surgery-error-cannot-detach-limb",
                    "body-part-detached" => "health-analyzer-surgery-error-body-part-detached",
                    "organ-already-in-body" => "health-analyzer-surgery-error-organ-already-in-body",
                    "limb-not-in-hand" => "health-analyzer-surgery-error-limb-not-in-hand",
                    "organ-not-in-body-part" => "health-analyzer-surgery-error-organ-not-in-body-part",
                    "organ-not-in-hand" => "health-analyzer-surgery-error-organ-not-in-hand",
                    "body-part-no-container" => "health-analyzer-surgery-error-body-part-no-container",
                    "no-slot-for-organ" => "health-analyzer-surgery-error-no-slot-for-organ",
                    "slot-filled" => "health-analyzer-surgery-error-slot-filled",
                    "slime-cannot-receive-implants" => "health-analyzer-surgery-error-slime-cannot-receive-implants",
                    "skeleton-cannot-receive-organs" => "health-analyzer-surgery-error-skeleton-cannot-receive-organs",
                    _ => "health-analyzer-surgery-error-invalid-surgical-process"
                };
                msg = Loc.GetString(msgKey);
            }
            _popupSystem.PopupEntity(msg, user, user, PopupType.Medium);
        }
    }

    private static string GetToolTagLocaleKey(string tagId)
    {
        return tagId switch
        {
            "CuttingTool" => "health-analyzer-surgery-tool-cutting",
            "SurgeryTool" => "health-analyzer-surgery-tool-surgery",
            "ManipulatingTool" => "health-analyzer-surgery-tool-manipulating",
            "SawingTool" => "health-analyzer-surgery-tool-sawing",
            "BluntTool" => "health-analyzer-surgery-tool-blunt",
            "SnippingTool" => "health-analyzer-surgery-tool-snipping",
            _ => tagId
        };
    }

    private void OnBodyOrganRemoved(Entity<BodyComponent> ent, ref OrganRemovedFromEvent args)
    {
        _limbRegeneration.OnOrganRemovedFrom(ent, ref args);

        var analyzerQuery = EntityQueryEnumerator<HealthAnalyzerComponent>();
        while (analyzerQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.ScannedEntity == ent.Owner)
            {
                UpdateScannedUser(uid, ent.Owner, true);
                break;
            }
        }
    }

    private void OnSurgeryPenaltyApplied(Entity<SurgeryLayerComponent> ent, ref SurgeryPenaltyAppliedEvent args)
    {
        if (!TryComp<BodyPartComponent>(ent.Owner, out var bodyPartComp) || bodyPartComp.Body is not { } body)
            return;

        var analyzerQuery = EntityQueryEnumerator<HealthAnalyzerComponent>();
        while (analyzerQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.ScannedEntity == body)
            {
                UpdateScannedUser(uid, body, true);
                break;
            }
        }
    }

    private void OnSurgeryUiRefreshRequest(Entity<BodyComponent> ent, ref SurgeryUiRefreshRequestEvent args)
    {
        var body = ent.Owner;
        var analyzerQuery = EntityQueryEnumerator<HealthAnalyzerComponent>();
        while (analyzerQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.ScannedEntity == body)
            {
                UpdateScannedUser(uid, body, true);
                break;
            }
        }
    }

    public override void Update(float frameTime)
    {
        var analyzerQuery = EntityQueryEnumerator<HealthAnalyzerComponent, TransformComponent>();
        while (analyzerQuery.MoveNext(out var uid, out var component, out var transform))
        {
            //Update rate limited to 1 second
            if (component.NextUpdate > _timing.CurTime)
                continue;

            if (component.ScannedEntity is not {} patient)
                continue;

            if (Deleted(patient))
            {
                StopAnalyzingEntity((uid, component), patient);
                continue;
            }

            component.NextUpdate = _timing.CurTime + component.UpdateInterval;

            //Get distance between health analyzer and the scanned entity
            //null is infinite range
            var patientCoordinates = Transform(patient).Coordinates;
            if (component.MaxScanRange != null && !_transformSystem.InRange(patientCoordinates, transform.Coordinates, component.MaxScanRange.Value))
            {
                //Range too far, disable updates until they are back in range
                PauseAnalyzingEntity((uid, component), patient);
                continue;
            }

            component.IsAnalyzerActive = true;
            UpdateScannedUser(uid, patient, true);
        }
    }

    /// <summary>
    /// Trigger the doafter for scanning
    /// </summary>
    private void OnAfterInteract(Entity<HealthAnalyzerComponent> uid, ref AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || !HasComp<MobStateComponent>(args.Target) || !_cell.HasDrawCharge(uid.Owner, user: args.User))
            return;

        _audio.PlayPvs(uid.Comp.ScanningBeginSound, uid);

        var doAfterCancelled = !_doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, uid.Comp.ScanDelay, new HealthAnalyzerDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            NeedHand = true,
            BreakOnMove = true,
        });

        if (args.Target == args.User || doAfterCancelled || uid.Comp.Silent)
            return;

        var msg = Loc.GetString("health-analyzer-popup-scan-target", ("user", Identity.Entity(args.User, EntityManager)));
        _popupSystem.PopupEntity(msg, args.Target.Value, args.Target.Value, PopupType.Medium);
    }

    private void OnDoAfter(Entity<HealthAnalyzerComponent> uid, ref HealthAnalyzerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null || !_cell.HasDrawCharge(uid.Owner, user: args.User))
            return;

        if (!uid.Comp.Silent)
            _audio.PlayPvs(uid.Comp.ScanningEndSound, uid);

        OpenUserInterface(args.User, uid);
        BeginAnalyzingEntity(uid, args.Target.Value);
        args.Handled = true;
    }

    /// <summary>
    /// Turn off when placed into a storage item or moved between slots/hands
    /// </summary>
    private void OnInsertedIntoContainer(Entity<HealthAnalyzerComponent> uid, ref EntGotInsertedIntoContainerMessage args)
    {
        if (uid.Comp.ScannedEntity is { } patient)
            _toggle.TryDeactivate(uid.Owner);
    }

    /// <summary>
    /// Disable continuous updates once turned off
    /// </summary>
    private void OnToggled(Entity<HealthAnalyzerComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated && ent.Comp.ScannedEntity is { } patient)
            StopAnalyzingEntity(ent, patient);
    }

    /// <summary>
    /// Turn off the analyser when dropped
    /// </summary>
    private void OnDropped(Entity<HealthAnalyzerComponent> uid, ref DroppedEvent args)
    {
        if (uid.Comp.ScannedEntity is { } patient)
            _toggle.TryDeactivate(uid.Owner);
    }

    private void OpenUserInterface(EntityUid user, EntityUid analyzer)
    {
        if (!_uiSystem.HasUi(analyzer, HealthAnalyzerUiKey.Key))
            return;

        _uiSystem.OpenUi(analyzer, HealthAnalyzerUiKey.Key, user);
    }

    /// <summary>
    /// Mark the entity as having its health analyzed, and link the analyzer to it
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer that should receive the updates</param>
    /// <param name="target">The entity to start analyzing</param>
    private void BeginAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target)
    {
        //Link the health analyzer to the scanned entity
        healthAnalyzer.Comp.ScannedEntity = target;

        var shared = EnsureComp<SharedHealthAnalyzerComponent>(healthAnalyzer);
        shared.ScannedEntity = target;
        Dirty(healthAnalyzer, shared);

        _toggle.TryActivate(healthAnalyzer.Owner);

        UpdateScannedUser(healthAnalyzer, target, true);
    }

    /// <summary>
    /// Remove the analyzer from the active list, and remove the component if it has no active analyzers
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer that's receiving the updates</param>
    /// <param name="target">The entity to analyze</param>
    private void StopAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target)
    {
        //Unlink the analyzer
        healthAnalyzer.Comp.ScannedEntity = null;

        if (TryComp<SharedHealthAnalyzerComponent>(healthAnalyzer, out var shared))
        {
            shared.ScannedEntity = null;
            Dirty(healthAnalyzer, shared);
        }

        _toggle.TryDeactivate(healthAnalyzer.Owner);

        UpdateScannedUser(healthAnalyzer, target, false);
    }


    /// <summary>
    /// If the scanner is active, sends one last update and sets it to inactive.
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer that's receiving the updates</param>
    /// <param name="target">The entity to analyze</param>
    private void PauseAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target)
    {
        if (!healthAnalyzer.Comp.IsAnalyzerActive)
            return;

        UpdateScannedUser(healthAnalyzer, target, false);
        healthAnalyzer.Comp.IsAnalyzerActive = false;
    }

    /// <summary>
    /// Send an update for the target to the healthAnalyzer
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer</param>
    /// <param name="target">The entity being scanned</param>
    /// <param name="scanMode">True makes the UI show ACTIVE, False makes the UI show INACTIVE</param>
    public void UpdateScannedUser(EntityUid healthAnalyzer, EntityUid target, bool scanMode)
    {
        if (!_uiSystem.HasUi(healthAnalyzer, HealthAnalyzerUiKey.Key)
            || !HasComp<DamageableComponent>(target))
            return;

        var uiState = GetHealthAnalyzerUiState(target);
        uiState.ScanMode = scanMode;

        _uiSystem.ServerSendUiMessage(
            healthAnalyzer,
            HealthAnalyzerUiKey.Key,
            new HealthAnalyzerScannedUserMessage(uiState)
        );
    }

    /// <summary>
    /// Creates a HealthAnalyzerState based on the current state of an entity.
    /// </summary>
    /// <param name="target">The entity being scanned</param>
    /// <returns></returns>
    public HealthAnalyzerUiState GetHealthAnalyzerUiState(EntityUid? target)
    {
        if (!target.HasValue || !HasComp<DamageableComponent>(target))
            return new HealthAnalyzerUiState();

        var entity = target.Value;
        var bodyTemperature = float.NaN;

        if (TryComp<TemperatureComponent>(entity, out var temp))
            bodyTemperature = temp.CurrentTemperature;

        var bloodAmount = float.NaN;
        var bleeding = false;
        var unrevivable = false;

        if (TryComp<BloodstreamComponent>(entity, out var bloodstream) &&
            _solutionContainerSystem.ResolveSolution(entity, bloodstream.BloodSolutionName,
                ref bloodstream.BloodSolution, out var bloodSolution))
        {
            bloodAmount = _bloodstreamSystem.GetBloodLevel(entity);
            bleeding = bloodstream.BleedAmount > 0;
        }

        if (TryComp<UnrevivableComponent>(entity, out var unrevivableComp) && unrevivableComp.Analyzable)
            unrevivable = true;

        var state = new HealthAnalyzerUiState(
            GetNetEntity(entity),
            bodyTemperature,
            bloodAmount,
            null,
            bleeding,
            unrevivable
        );

        if (TryComp<BodyComponent>(entity, out var body))
        {
            var query = new BodyPartQueryEvent(entity);
            RaiseLocalEvent(entity, ref query);
            state.BodyParts = query.Parts.Select(e => GetNetEntity(e)).ToList();

            var totalEv = new IntegrityPenaltyTotalRequestEvent(entity);
            RaiseLocalEvent(entity, ref totalEv);
            var usage = TryComp<IntegrityUsageComponent>(entity, out var usageComp) ? usageComp.Usage : 0;
            state.IntegrityTotal = totalEv.Total + usage;
            state.IntegrityMax = TryComp<IntegrityCapacityComponent>(entity, out var cap) ? cap.MaxIntegrity : 6;

            state.IntegrityPenaltyEntries ??= new List<IntegrityPenaltyDisplayEntry>();
            state.IntegrityPenaltyEntries.Clear();
            var nonCyberUsage = 0;
            foreach (var organ in _body.GetAllOrgans(entity))
            {
                if (TryComp<OrganComponent>(organ, out var organComp) && organComp.IntegrityCost > 0)
                    nonCyberUsage += organComp.IntegrityCost;
            }
            if (nonCyberUsage > 0)
                state.IntegrityPenaltyEntries.Add(new IntegrityPenaltyDisplayEntry { Description = "health-analyzer-integrity-usage", Amount = nonCyberUsage });

            foreach (var organ in _body.GetAllOrgans(entity))
            {
                if (!TryComp<IntegrityPenaltyComponent>(organ, out var penalty) || penalty.Penalty <= 0)
                    continue;

                var desc = TryComp<OrganComponent>(organ, out var organComp) && organComp.Category is { } cat
                    ? GetCategoryDisplayName(cat)
                    : Identity.Name(organ, EntityManager);

                if (TryComp<SurgeryLayerComponent>(organ, out var layerComp))
                {
                    var children = new List<IntegrityPenaltyDisplayEntry>();
                    foreach (var stepId in layerComp.PerformedSkinSteps.Concat(layerComp.PerformedTissueSteps).Concat(layerComp.PerformedOrganSteps))
                    {
                        string? stepName = null;
                        var stepPenalty = 0;
                        if (_prototypes.TryIndex<SurgeryStepPrototype>(stepId, out var step))
                        {
                            stepName = step.Name?.Id ?? stepId;
                            stepPenalty = step.Penalty;
                        }
                        else if (_prototypes.TryIndex<SurgeryProcedurePrototype>(stepId, out var proc))
                        {
                            stepName = proc.Name?.Id ?? stepId;
                            stepPenalty = proc.Penalty;
                        }
                        if (stepName == null || stepPenalty <= 0)
                            continue;
                        children.Add(new IntegrityPenaltyDisplayEntry { Description = stepName, Amount = stepPenalty });
                    }
                    state.IntegrityPenaltyEntries.Add(new IntegrityPenaltyDisplayEntry
                    {
                        Description = desc ?? "?",
                        Amount = penalty.Penalty,
                        Children = children.Count > 0 ? children : null
                    });
                }
                else
                {
                    state.IntegrityPenaltyEntries.Add(new IntegrityPenaltyDisplayEntry { Description = desc ?? "?", Amount = penalty.Penalty });
                }
            }
            if (TryComp<IntegritySurgeryComponent>(entity, out var surgeryComp))
            {
                foreach (var entry in surgeryComp.Entries)
                {
                    if (entry.Category == IntegrityPenaltyCategory.ImproperTools)
                    {
                        MergeImproperToolsIntoOrganEntry(state.IntegrityPenaltyEntries, entry);
                    }
                    else
                    {
                        state.IntegrityPenaltyEntries.Add(ConvertToDisplayEntry(entry));
                    }
                }
            }

            foreach (var part in query.Parts)
            {
                if (TryComp<SurgeryLayerComponent>(part, out var layer))
                {
                    var categoryId = TryComp<OrganComponent>(part, out var organ) ? organ.Category?.ToString() : null;
                    var stepsConfig = _surgeryLayer.GetStepsConfig(entity, part);
                    // Fallback for grafted limbs: if limb's species config is null, use body's species so DetachLimb/InsertOrgan show
                    if (stepsConfig == null && organ != null && organ.Category is { } cat && TryComp<HumanoidProfileComponent>(entity, out var bodyProfile))
                        stepsConfig = _surgeryLayer.GetStepsConfig(bodyProfile.Species, cat);

                    var skinProcedures = new List<SurgeryProcedureState>();
                    var tissueProcedures = new List<SurgeryProcedureState>();
                    bool skinOpen = false, tissueOpen = false, organOpen = false;

                    if (stepsConfig != null)
                    {
                        foreach (var stepId in stepsConfig.GetSkinOpenStepIds(_prototypes))
                            skinProcedures.Add(new SurgeryProcedureState { StepId = stepId, Performed = _surgeryLayer.IsStepPerformed((part, layer), stepId) });
                        foreach (var stepId in stepsConfig.GetSkinCloseStepIds(_prototypes))
                            skinProcedures.Add(new SurgeryProcedureState { StepId = stepId, Performed = _surgeryLayer.IsStepPerformed((part, layer), stepId) });
                        foreach (var stepId in stepsConfig.GetTissueOpenStepIds(_prototypes))
                            tissueProcedures.Add(new SurgeryProcedureState { StepId = stepId, Performed = _surgeryLayer.IsStepPerformed((part, layer), stepId) });
                        foreach (var stepId in stepsConfig.GetTissueCloseStepIds(_prototypes))
                            tissueProcedures.Add(new SurgeryProcedureState { StepId = stepId, Performed = _surgeryLayer.IsStepPerformed((part, layer), stepId) });

                        skinOpen = _surgeryLayer.IsSkinOpen(layer, stepsConfig);
                        tissueOpen = _surgeryLayer.IsTissueOpen(layer, stepsConfig);
                        organOpen = _surgeryLayer.IsOrganLayerOpen(layer, stepsConfig);
                    }

                    var availableStepIds = _surgeryLayer.GetAvailableSteps(entity, part).ToList();
                    var availableOrganSteps = _surgeryLayer.GetAvailableOrganSteps(entity, part)
                        .Select(x => new OrganStepAvailability { StepId = x.StepId, Organ = x.Organ }).ToList();
                    var orderedSkinStepIds = _surgeryLayer.GetAllStepsInOrder(entity, part, SurgeryLayer.Skin).ToList();
                    var orderedTissueStepIds = _surgeryLayer.GetAllStepsInOrder(entity, part, SurgeryLayer.Tissue).ToList();

                    var layerData = new SurgeryLayerStateData
                    {
                        BodyPart = GetNetEntity(part),
                        CategoryId = categoryId,
                        SkinProcedures = skinProcedures,
                        TissueProcedures = tissueProcedures,
                        SkinOpen = skinOpen,
                        TissueOpen = tissueOpen,
                        OrganOpen = organOpen,
                        OrganProcedures = _surgeryLayer.GetPerformedSteps((part, layer), SurgeryLayer.Organ).Select(s => new SurgeryProcedureState { StepId = s, Performed = true }).ToList(),
                        AvailableStepIds = availableStepIds,
                        OrderedSkinStepIds = orderedSkinStepIds,
                        OrderedTissueStepIds = orderedTissueStepIds,
                        AvailableOrganSteps = availableOrganSteps
                    };
                    if (TryComp<BodyPartComponent>(part, out var bodyPartComp) && bodyPartComp.Organs != null)
                    {
                        foreach (var child in bodyPartComp.Organs.ContainedEntities)
                        {
                            if (TryComp<OrganComponent>(child, out var childOrgan))
                            {
                                layerData.Organs.Add(new OrganInBodyPartData
                                {
                                    Organ = GetNetEntity(child),
                                    CategoryId = childOrgan.Category?.ToString()
                                });
                            }
                        }
                        foreach (var slot in bodyPartComp.Slots)
                        {
                            var slotId = slot.ToString();
                            var filled = layerData.Organs.Any(o => o.CategoryId == slotId);
                            if (!filled)
                                layerData.EmptySlots.Add(slotId);
                        }
                    }
                    state.BodyPartLayerState.Add(layerData);
                }
            }

            if (TryComp<HumanoidProfileComponent>(entity, out var profile))
            {
                var limbCategories = new[] { "ArmLeft", "ArmRight", "LegLeft", "LegRight" };
                var presentCategories = new HashSet<string>();
                foreach (var part in query.Parts)
                {
                    if (TryComp<OrganComponent>(part, out var organ) && organ.Category is { } cat)
                        presentCategories.Add(cat.ToString());
                }
                foreach (var categoryId in limbCategories)
                {
                    if (presentCategories.Contains(categoryId))
                        continue;
                    var availableStepIds = _surgeryLayer.GetAvailableStepsForEmptySlot(entity, categoryId).ToList();
                    state.BodyPartLayerState.Add(new SurgeryLayerStateData
                    {
                        BodyPart = GetNetEntity(entity),
                        CategoryId = categoryId,
                        SkinProcedures = new List<SurgeryProcedureState>(),
                        TissueProcedures = new List<SurgeryProcedureState>(),
                        SkinOpen = false,
                        TissueOpen = false,
                        OrganOpen = true,
                        OrganProcedures = new List<SurgeryProcedureState>(),
                        AvailableStepIds = availableStepIds,
                        AvailableOrganSteps = new List<OrganStepAvailability>()
                    });
                }
            }
        }

        return state;
    }

    private static IntegrityPenaltyDisplayEntry ConvertToDisplayEntry(IntegrityPenaltyEntry entry)
    {
        List<IntegrityPenaltyDisplayEntry>? children = null;
        if (entry.Children != null && entry.Children.Count > 0)
        {
            children = entry.Children.Select(c => ConvertToDisplayEntry(c)).ToList();
        }
        return new IntegrityPenaltyDisplayEntry
        {
            Description = entry.Reason,
            Amount = entry.Amount,
            Children = children
        };
    }

    private static void MergeImproperToolsIntoOrganEntry(List<IntegrityPenaltyDisplayEntry> entries, IntegrityPenaltyEntry improperEntry)
    {
        var organDesc = improperEntry.Reason;
        var improvisedAmount = 0;
        if (improperEntry.Children != null)
        {
            foreach (var stepChild in improperEntry.Children)
                improvisedAmount += stepChild.Children?.Sum(c => c.Amount) ?? 0;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var organEntry = entries[i];
            if (organEntry.Description != organDesc)
                continue;

            var newAmount = organEntry.Amount + improvisedAmount;
            var children = organEntry.Children != null ? new List<IntegrityPenaltyDisplayEntry>(organEntry.Children) : new List<IntegrityPenaltyDisplayEntry>();

            if (improperEntry.Children != null)
            {
                foreach (var stepChild in improperEntry.Children)
                {
                    var improvisedChildren = stepChild.Children != null
                        ? stepChild.Children.Select(c => ConvertToDisplayEntry(c)).ToList()
                        : null;
                    var stepDisplay = new IntegrityPenaltyDisplayEntry
                    {
                        Description = stepChild.Reason,
                        Amount = stepChild.Amount,
                        Children = improvisedChildren
                    };
                    var stepIdx = children.FindIndex(c => c.Description == stepChild.Reason);
                    if (stepIdx >= 0)
                    {
                        children[stepIdx] = stepDisplay;
                    }
                    else
                    {
                        children.Add(stepDisplay);
                    }
                }
            }

            entries[i] = new IntegrityPenaltyDisplayEntry
            {
                Description = organEntry.Description,
                Amount = newAmount,
                Children = children.Count > 0 ? children : null
            };
            return;
        }

        entries.Add(ConvertToDisplayEntry(improperEntry));
    }

    private string GetCategoryDisplayName(ProtoId<OrganCategoryPrototype> category)
    {
        if (_prototypes.TryIndex(category, out var proto) && proto.Name is { } name)
            return Loc.GetString(name);
        return category.ToString();
    }
}
