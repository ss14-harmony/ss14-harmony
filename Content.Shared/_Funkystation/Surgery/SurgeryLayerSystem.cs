using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Humanoid;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Surgery layer state is initialized by BodySystem.OnBodyPartInit.
/// Provides config-driven layer state and step validation.
/// </summary>
public sealed class SurgeryLayerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    /// <summary>
    /// Resolves BodyPartSurgeryStepsPrototype for the given species and organ category.
    /// Excludes CyberLimb* configs (they are only used via StepsConfigId on the body part).
    /// Falls back to Human prototype when no config exists for the species (e.g. new mod species).
    /// </summary>
    public BodyPartSurgeryStepsPrototype? GetStepsConfig(ProtoId<Humanoid.Prototypes.SpeciesPrototype> speciesId, ProtoId<OrganCategoryPrototype> organCategory)
    {
        foreach (var proto in _prototypes.EnumeratePrototypes<BodyPartSurgeryStepsPrototype>())
        {
            if (proto.ID.StartsWith("CyberLimb"))
                continue;
            if (proto.SpeciesId == speciesId && proto.OrganCategory == organCategory)
                return proto;
        }
        return _prototypes.TryIndex(new ProtoId<BodyPartSurgeryStepsPrototype>("Human" + organCategory), out var humanProto) ? humanProto : null;
    }

    /// <summary>
    /// Resolves steps config for a body part. Uses SurgeryBodyPartComponent if present, else body's species and part's OrganComponent.
    /// When StepsConfigId is set (e.g. for cyber limbs), resolves that prototype directly.
    /// </summary>
    public BodyPartSurgeryStepsPrototype? GetStepsConfig(EntityUid body, EntityUid bodyPart)
    {
        if (TryComp<SurgeryBodyPartComponent>(bodyPart, out var surgeryBodyPart))
        {
            if (surgeryBodyPart.StepsConfigId is { } stepsConfigId &&
                _prototypes.TryIndex(stepsConfigId, out BodyPartSurgeryStepsPrototype? stepsConfig))
                return stepsConfig;
            return GetStepsConfig(surgeryBodyPart.SpeciesId, surgeryBodyPart.OrganCategory);
        }

        if (!TryComp<HumanoidProfileComponent>(body, out var humanoidProfile) || !TryComp<OrganComponent>(bodyPart, out var organ) || organ.Category is not { } category)
            return null;

        return GetStepsConfig(humanoidProfile.Species, category);
    }

    /// <summary>
    /// Returns whether the skin layer is open (all skinOpenSteps done, no skinCloseSteps done).
    /// </summary>
    public bool IsSkinOpen(SurgeryLayerComponent layerComp, BodyPartSurgeryStepsPrototype stepsConfig)
    {
        var skinOpen = stepsConfig.GetSkinOpenStepIds(_prototypes);
        var skinClose = stepsConfig.GetSkinCloseStepIds(_prototypes);
        return skinOpen.All(s => layerComp.PerformedSkinSteps.Contains(s))
            && !skinClose.Any(s => layerComp.PerformedSkinSteps.Contains(s));
    }

    /// <summary>
    /// Returns whether the tissue layer is open (skin open + all tissueOpenSteps done, no tissueCloseSteps done).
    /// </summary>
    public bool IsTissueOpen(SurgeryLayerComponent layerComp, BodyPartSurgeryStepsPrototype stepsConfig)
    {
        if (!IsSkinOpen(layerComp, stepsConfig))
            return false;
        var tissueOpen = stepsConfig.GetTissueOpenStepIds(_prototypes);
        var tissueClose = stepsConfig.GetTissueCloseStepIds(_prototypes);
        return tissueOpen.All(s => layerComp.PerformedTissueSteps.Contains(s))
            && !tissueClose.Any(s => layerComp.PerformedTissueSteps.Contains(s));
    }

    /// <summary>
    /// Returns whether the organ layer is open (tissue open; organ has no close steps).
    /// </summary>
    public bool IsOrganLayerOpen(SurgeryLayerComponent layerComp, BodyPartSurgeryStepsPrototype stepsConfig)
    {
        return IsTissueOpen(layerComp, stepsConfig);
    }

    /// <summary>
    /// Returns whether the given step can be performed based on declarative prerequisites.
    /// For organ procedures (removal/insertion), pass organ to check per-organ progress.
    /// </summary>
    public bool CanPerformStep(string stepId, SurgeryLayer layer, SurgeryLayerComponent layerComp, BodyPartSurgeryStepsPrototype stepsConfig, EntityUid? bodyPart = null, EntityUid? organ = null)
    {
        if (_prototypes.TryIndex<SurgeryProcedurePrototype>(stepId, out var procedure))
        {
            if (procedure.Layer != layer)
                return false;
            if (!IsStepAllowedForBodyPart(stepId, layer, stepsConfig, bodyPart))
                return false;
            return EvaluatePrerequisites(procedure.Prerequisites, layerComp, stepsConfig, bodyPart, organ);
        }
        if (_prototypes.TryIndex<SurgeryStepPrototype>(stepId, out var step))
        {
            if (step.Layer != layer)
                return false;
            if (!IsStepAllowedForBodyPart(stepId, layer, stepsConfig, bodyPart))
                return false;
            return EvaluatePrerequisites(step.Prerequisites, layerComp, stepsConfig, bodyPart, organ);
        }
        return false;
    }

    /// <summary>
    /// Returns whether the step is in this body part's catalog (from stepsConfig or organ procedures).
    /// </summary>
    public bool IsStepAllowedForBodyPart(string stepId, SurgeryLayer layer, BodyPartSurgeryStepsPrototype stepsConfig, EntityUid? bodyPart = null)
    {
        return layer switch
        {
            SurgeryLayer.Skin => stepsConfig.GetSkinOpenStepIds(_prototypes).Contains(stepId)
                || stepsConfig.GetSkinCloseStepIds(_prototypes).Contains(stepId),
            SurgeryLayer.Tissue => stepsConfig.GetTissueOpenStepIds(_prototypes).Contains(stepId)
                || stepsConfig.GetTissueCloseStepIds(_prototypes).Contains(stepId),
            SurgeryLayer.Organ => stepsConfig.GetOrganStepIds(_prototypes).Contains(stepId)
                || (bodyPart.HasValue && IsOrganProcedureInBodyPart(bodyPart.Value, stepId)),
            _ => false
        };
    }

    private bool IsOrganProcedureInBodyPart(EntityUid bodyPart, string procedureId)
    {
        if (!TryComp<BodyPartComponent>(bodyPart, out var bodyPartComp) || bodyPartComp.Organs == null)
            return false;
        foreach (var organ in bodyPartComp.Organs.ContainedEntities)
        {
            if (TryComp<OrganSurgeryProceduresComponent>(organ, out var procs) &&
                (procs.RemovalProcedures.Any(p => p.ToString() == procedureId) || procs.InsertionProcedures.Any(p => p.ToString() == procedureId)))
                return true;
        }
        return false;
    }

    private bool EvaluatePrerequisites(IReadOnlyList<StepPrerequisite> prereqs, SurgeryLayerComponent comp, BodyPartSurgeryStepsPrototype config, EntityUid? bodyPart = null, EntityUid? organ = null)
    {
        foreach (var p in prereqs)
        {
            if (!EvaluateOne(p, comp, config, bodyPart, organ))
                return false;
        }
        return true;
    }

    private bool EvaluateOne(StepPrerequisite p, SurgeryLayerComponent comp, BodyPartSurgeryStepsPrototype config, EntityUid? bodyPart = null, EntityUid? organ = null)
    {
        switch (p.Type)
        {
            case StepPrerequisiteType.RequireLayerOpen:
                if (p.Layer is not { } layer)
                    return false;
                return IsLayerOpen(comp, config, layer);
            case StepPrerequisiteType.RequireLayerClosed:
                if (p.Layer is not { } layerClosed)
                    return false;
                return !IsLayerOpen(comp, config, layerClosed);
            case StepPrerequisiteType.RequireStepPerformed:
                var reqId = p.Procedure?.ToString() ?? p.StepId;
                if (string.IsNullOrEmpty(reqId))
                    return false;
                SurgeryLayer reqLayer;
                if (p.Procedure.HasValue && _prototypes.TryIndex(p.Procedure.Value, out SurgeryProcedurePrototype? proc))
                    reqLayer = proc.Layer;
                else if (!string.IsNullOrEmpty(p.StepId) && _prototypes.TryIndex<SurgeryStepPrototype>(p.StepId, out var reqStep))
                    reqLayer = reqStep.Layer;
                else
                    return false;
                if (reqLayer == SurgeryLayer.Organ && organ.HasValue)
                {
                    var organNet = GetNetEntity(organ.Value);
                    if (IsProcedureInOrganRemoval(bodyPart, organ.Value, reqId))
                        return GetOrganRemovalSteps(comp, organNet).Contains(reqId);
                    if (IsProcedureInOrganInsertion(bodyPart, organ.Value, reqId))
                        return GetOrganInsertSteps(comp, organNet).Contains(reqId);
                }
                var performedList = reqLayer switch
                {
                    SurgeryLayer.Skin => comp.PerformedSkinSteps,
                    SurgeryLayer.Tissue => comp.PerformedTissueSteps,
                    SurgeryLayer.Organ => comp.PerformedOrganSteps,
                    _ => null
                };
                return performedList != null && performedList.Contains(reqId);
            default:
                return false;
        }
    }

    private bool IsProcedureInOrganRemoval(EntityUid? bodyPart, EntityUid organ, string procedureId)
    {
        if (!bodyPart.HasValue || !TryComp<OrganSurgeryProceduresComponent>(organ, out var procs))
            return false;
        return procs.RemovalProcedures.Any(p => p.ToString() == procedureId);
    }

    private bool IsProcedureInOrganInsertion(EntityUid? bodyPart, EntityUid organ, string procedureId)
    {
        if (!bodyPart.HasValue || !TryComp<OrganSurgeryProceduresComponent>(organ, out var procs))
            return false;
        return procs.InsertionProcedures.Any(p => p.ToString() == procedureId);
    }

    private IReadOnlyList<string> GetOrganRemovalSteps(SurgeryLayerComponent comp, NetEntity organ)
    {
        var entry = comp.OrganRemovalProgress.FirstOrDefault(e => e.Organ == organ);
        return entry?.Steps ?? new List<string>();
    }

    private IReadOnlyList<string> GetOrganInsertSteps(SurgeryLayerComponent comp, NetEntity organ)
    {
        var entry = comp.OrganInsertProgress.FirstOrDefault(e => e.Organ == organ);
        return entry?.Steps ?? new List<string>();
    }

    private bool IsOrganProcedureFromOrganPrototype(EntityUid bodyPart, string stepId)
    {
        if (!TryComp<BodyPartComponent>(bodyPart, out var bodyPartComp) || bodyPartComp.Organs == null)
            return false;
        foreach (var organ in bodyPartComp.Organs.ContainedEntities)
        {
            if (TryComp<OrganSurgeryProceduresComponent>(organ, out var procs) &&
                (procs.RemovalProcedures.Any(p => p.ToString() == stepId) || procs.InsertionProcedures.Any(p => p.ToString() == stepId)))
                return true;
        }
        return false;
    }

    private bool IsLayerOpen(SurgeryLayerComponent comp, BodyPartSurgeryStepsPrototype config, SurgeryLayer layer)
    {
        return layer switch
        {
            SurgeryLayer.Skin => IsSkinOpen(comp, config),
            SurgeryLayer.Tissue => IsTissueOpen(comp, config),
            SurgeryLayer.Organ => IsOrganLayerOpen(comp, config),
            _ => false
        };
    }

    /// <summary>
    /// Returns the list of step IDs that can be performed on this body part.
    /// First steps of skin and tissue layers are always available when their layer is accessible, so surgery never gets locked.
    /// </summary>
    public IReadOnlyList<string> GetAvailableSteps(EntityUid body, EntityUid bodyPart, SurgeryLayer? filterLayer = null)
    {
        var stepsConfig = GetStepsConfig(body, bodyPart);
        if (stepsConfig == null)
            return Array.Empty<string>();

        var layerComp = CompOrNull<SurgeryLayerComponent>(bodyPart);
        if (layerComp == null)
            return Array.Empty<string>();

        var allSteps = GetAllStepsForBodyPart(body, bodyPart, stepsConfig);
        var result = new List<string>();
        foreach (var stepId in allSteps)
        {
            if (IsOrganProcedureFromOrganPrototype(bodyPart, stepId))
                continue;
            SurgeryLayer stepLayer;
            if (_prototypes.TryIndex<SurgeryProcedurePrototype>(stepId, out var procedure))
                stepLayer = procedure.Layer;
            else if (_prototypes.TryIndex<SurgeryStepPrototype>(stepId, out var step))
                stepLayer = step.Layer;
            else
                continue;
            if (filterLayer.HasValue && stepLayer != filterLayer.Value)
                continue;
            if (IsStepPerformed((bodyPart, layerComp), stepId))
                continue;
            if (CanPerformStep(stepId, stepLayer, layerComp, stepsConfig, bodyPart))
                result.Add(stepId);
        }

        // Safeguard: ensure first steps are always available when layer is accessible, so surgery never gets locked
        if (result.Count == 0)
        {
            var skinOpen = stepsConfig.GetSkinOpenStepIds(_prototypes);
            var skinClose = stepsConfig.GetSkinCloseStepIds(_prototypes);
            var tissueOpen = stepsConfig.GetTissueOpenStepIds(_prototypes);
            var skinIsOpen = skinOpen.All(s => layerComp.PerformedSkinSteps.Contains(s))
                && !skinClose.Any(s => layerComp.PerformedSkinSteps.Contains(s));

            if (!skinIsOpen && skinOpen.Count > 0)
            {
                var firstSkinStep = skinOpen[0];
                if (!IsStepPerformed((bodyPart, layerComp), firstSkinStep)
                    && (!filterLayer.HasValue || _prototypes.TryIndex<SurgeryProcedurePrototype>(firstSkinStep, out var sp) && sp.Layer == filterLayer.Value
                        || _prototypes.TryIndex<SurgeryStepPrototype>(firstSkinStep, out var ss) && ss.Layer == filterLayer.Value))
                    result.Add(firstSkinStep);
            }
            else if (skinIsOpen && tissueOpen.Count > 0)
            {
                var firstTissueStep = tissueOpen[0];
                if (!IsStepPerformed((bodyPart, layerComp), firstTissueStep)
                    && (!filterLayer.HasValue || _prototypes.TryIndex<SurgeryProcedurePrototype>(firstTissueStep, out var tp) && tp.Layer == filterLayer.Value
                        || _prototypes.TryIndex<SurgeryStepPrototype>(firstTissueStep, out var ts) && ts.Layer == filterLayer.Value))
                    result.Add(firstTissueStep);
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the full ordered list of procedure/step IDs for the body part, optionally filtered by layer.
    /// Used by UI to show all steps in fixed order with unavailable ones greyed out.
    /// </summary>
    public IReadOnlyList<string> GetAllStepsInOrder(EntityUid body, EntityUid bodyPart, SurgeryLayer? filterLayer = null)
    {
        var stepsConfig = GetStepsConfig(body, bodyPart);
        if (stepsConfig == null)
            return Array.Empty<string>();

        var allSteps = GetAllStepsForBodyPart(body, bodyPart, stepsConfig).ToList();
        if (!filterLayer.HasValue)
            return allSteps;

        var result = new List<string>();
        foreach (var stepId in allSteps)
        {
            if (IsOrganProcedureFromOrganPrototype(bodyPart, stepId))
                continue;
            SurgeryLayer stepLayer;
            if (_prototypes.TryIndex<SurgeryProcedurePrototype>(stepId, out var procedure))
                stepLayer = procedure.Layer;
            else if (_prototypes.TryIndex<SurgeryStepPrototype>(stepId, out var step))
                stepLayer = step.Layer;
            else
                continue;
            if (stepLayer == filterLayer.Value)
                result.Add(stepId);
        }
        return result;
    }

    private IEnumerable<string> GetAllStepsForBodyPart(EntityUid body, EntityUid bodyPart, BodyPartSurgeryStepsPrototype config)
    {
        var skinOpen = config.GetSkinOpenStepIds(_prototypes);
        var skinClose = config.GetSkinCloseStepIds(_prototypes);
        var tissueOpen = config.GetTissueOpenStepIds(_prototypes);
        var tissueClose = config.GetTissueCloseStepIds(_prototypes);
        var organ = config.GetOrganStepIds(_prototypes).ToList();

        // Add organ removal/insertion procedures from organs in this body part
        
        if (TryComp<BodyPartComponent>(bodyPart, out var bodyPartComp) && bodyPartComp.Organs != null)
        {
            foreach (var organEntity in bodyPartComp.Organs.ContainedEntities)
            {
                if (TryComp<OrganComponent>(organEntity, out var organComp) && organComp.Category is { } category)
                {
                    var categoryId = category.Id;
                    // Tongue and Ears surgeries disabled pending a reason to do those surgeries
                    // Hardcoded for now because I couldn't find a better way to disable this
                    if (categoryId == "Tongue" || categoryId == "Ears")
                        continue;
                }

                if (TryComp<OrganSurgeryProceduresComponent>(organEntity, out var organProcs))
                {
                    foreach (var proc in organProcs.RemovalProcedures)
                        organ.Add(proc.ToString());
                    foreach (var proc in organProcs.InsertionProcedures)
                        organ.Add(proc.ToString());
                }
            }
        }

        return skinOpen.Concat(skinClose).Concat(tissueOpen).Concat(tissueClose).Concat(organ).Distinct();
    }

    /// <summary>
    /// Returns available steps for an empty limb slot (e.g. after DetachLimb).
    /// Only AttachLimb is relevant; no layer opening is required.
    /// </summary>
    public IReadOnlyList<string> GetAvailableStepsForEmptySlot(EntityUid body, string categoryId)
    {
        if (!TryComp<HumanoidProfileComponent>(body, out var humanoidProfile))
            return Array.Empty<string>();

        var category = new ProtoId<OrganCategoryPrototype>(categoryId);
        var stepsConfig = GetStepsConfig(humanoidProfile.Species, category);
        if (stepsConfig == null)
            return Array.Empty<string>();

        if (!stepsConfig.GetOrganStepIds(_prototypes).Contains("AttachLimb"))
            return Array.Empty<string>();

        return new[] { "AttachLimb" };
    }

    /// <summary>
    /// Returns whether the given step has been performed on this body part.
    /// </summary>
    public bool IsStepPerformed(Entity<SurgeryLayerComponent> ent, string stepId)
    {
        var comp = ent.Comp;
        return comp.PerformedSkinSteps.Contains(stepId)
            || comp.PerformedTissueSteps.Contains(stepId)
            || comp.PerformedOrganSteps.Contains(stepId);
    }

    /// <summary>
    /// Returns the list of performed step IDs for the given layer.
    /// </summary>
    public IReadOnlyList<string> GetPerformedSteps(Entity<SurgeryLayerComponent> ent, SurgeryLayer layer)
    {
        var comp = ent.Comp;
        return layer switch
        {
            SurgeryLayer.Skin => comp.PerformedSkinSteps,
            SurgeryLayer.Tissue => comp.PerformedTissueSteps,
            SurgeryLayer.Organ => comp.PerformedOrganSteps,
            _ => Array.Empty<string>()
        };
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryLayerComponent, SurgeryStepRequestEvent>(OnSurgeryStepRequest);
    }

    private void OnSurgeryStepRequest(Entity<SurgeryLayerComponent> ent, ref SurgeryStepRequestEvent args)
    {
        if (args.StepsConfig == null)
        {
            args.Valid = false;
            args.RejectReason = "unknown-species-or-category";
            return;
        }

        if (!CanPerformStep(args.ProcedureId.ToString(), args.Layer, ent.Comp, args.StepsConfig, ent.Owner, args.Organ))
        {
            args.Valid = false;
            args.RejectReason = "layer-not-open";
            return;
        }
    }

    /// <summary>
    /// Returns available organ procedures as (stepId, organ) pairs for organ removal/insertion flows.
    /// </summary>
    public IReadOnlyList<(string StepId, NetEntity Organ)> GetAvailableOrganSteps(EntityUid body, EntityUid bodyPart)
    {
        var stepsConfig = GetStepsConfig(body, bodyPart);
        if (stepsConfig == null)
            return Array.Empty<(string, NetEntity)>();

        var layerComp = CompOrNull<SurgeryLayerComponent>(bodyPart);
        if (layerComp == null)
            return Array.Empty<(string, NetEntity)>();

        var result = new List<(string, NetEntity)>();
        if (!TryComp<BodyPartComponent>(bodyPart, out var bodyPartComp) || bodyPartComp.Organs == null)
            return result;

        foreach (var organ in bodyPartComp.Organs.ContainedEntities)
        {
            // Tongue and Ears surgeries disabled pending a reason to do those surgeries (no speech/hearing mechanics).
            if (TryComp<OrganComponent>(organ, out var organComp) && organComp.Category is { } category)
            {
                var categoryId = category.Id;
                if (categoryId == "Tongue" || categoryId == "Ears")
                    continue;
            }

            var organNet = GetNetEntity(organ);
            if (!TryComp<OrganSurgeryProceduresComponent>(organ, out var organProcs))
                continue;
            // Organs that were surgically detached store their removal state. When re-inserted, only show repair steps.
            var removalAlreadyDone = TryComp<OrganRemovedSurgeryStateComponent>(organ, out var removedState) &&
                organProcs.RemovalProcedures.All(p => removedState.PerformedRemovalSteps.Contains(p.ToString()));
            if (!removalAlreadyDone)
            {
                foreach (var proc in organProcs.RemovalProcedures)
                {
                    var stepId = proc.ToString();
                    if (IsOrganStepPerformed(layerComp, stepId, organNet, forRemoval: true))
                        continue;
                    if (CanPerformStep(stepId, SurgeryLayer.Organ, layerComp, stepsConfig, bodyPart, organ))
                        result.Add((stepId, organNet));
                }
            }
            // Insertion procedures (Hemostat, Searing, etc.) are only for organs that were just inserted.
            // Organs without an OrganInsertProgress entry are native to the body and don't need mend steps.
            if (!layerComp.OrganInsertProgress.Any(e => e.Organ == organNet))
                continue;
            foreach (var proc in organProcs.InsertionProcedures)
            {
                var stepId = proc.ToString();
                if (IsOrganStepPerformed(layerComp, stepId, organNet, forRemoval: false))
                    continue;
                if (CanPerformStep(stepId, SurgeryLayer.Organ, layerComp, stepsConfig, bodyPart, organ))
                    result.Add((stepId, organNet));
            }
        }
        return result;
    }

    private bool IsOrganStepPerformed(SurgeryLayerComponent comp, string stepId, NetEntity organ, bool forRemoval)
    {
        var list = forRemoval ? GetOrganRemovalSteps(comp, organ) : GetOrganInsertSteps(comp, organ);
        return list.Contains(stepId);
    }
}
