using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Prototypes;

/// <summary>
/// Defines the ordered surgical procedures for a body part of a given species.
/// One per (SpeciesId, OrganCategory) - e.g. HumanArmLeft, DionaLegRight.
/// </summary>
[Prototype]
public sealed partial class BodyPartSurgeryStepsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Species this config applies to (e.g. Human, Diona).
    /// </summary>
    [DataField("species", required: true)]
    public ProtoId<SpeciesPrototype> SpeciesId { get; private set; }

    /// <summary>
    /// Organ category (e.g. Torso, ArmLeft, LegRight). Uses OrganCategoryPrototype.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<OrganCategoryPrototype> OrganCategory { get; private set; }

    /// <summary>
    /// Ordered list of skin opening steps (procedure + optional flavor text). When empty, falls back to SkinOpenSteps.
    /// </summary>
    [DataField]
    public List<BodyPartSurgeryStepEntry> SkinOpenStepEntries { get; private set; } = new();

    /// <summary>
    /// Ordered list of skin opening procedure IDs (last one gates tissue layer). Used when SkinOpenStepEntries is empty.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> SkinOpenSteps { get; private set; } = new();

    /// <summary>
    /// Ordered list of skin closing steps. When empty, falls back to SkinCloseSteps.
    /// </summary>
    [DataField]
    public List<BodyPartSurgeryStepEntry> SkinCloseStepEntries { get; private set; } = new();

    /// <summary>
    /// Ordered list of skin closing procedure IDs (any done = skin closed). Used when SkinCloseStepEntries is empty.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> SkinCloseSteps { get; private set; } = new();

    /// <summary>
    /// Ordered list of tissue opening steps. When empty, falls back to TissueOpenSteps.
    /// </summary>
    [DataField]
    public List<BodyPartSurgeryStepEntry> TissueOpenStepEntries { get; private set; } = new();

    /// <summary>
    /// Ordered list of tissue opening procedure IDs (last one gates organ layer). Used when TissueOpenStepEntries is empty.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> TissueOpenSteps { get; private set; } = new();

    /// <summary>
    /// Ordered list of tissue closing steps. When empty, falls back to TissueCloseSteps.
    /// </summary>
    [DataField]
    public List<BodyPartSurgeryStepEntry> TissueCloseStepEntries { get; private set; } = new();

    /// <summary>
    /// Ordered list of tissue closing procedure IDs (any done = tissue closed). Used when TissueCloseStepEntries is empty.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> TissueCloseSteps { get; private set; } = new();

    /// <summary>
    /// Organ procedure IDs for limbs (DetachLimb, AttachLimb). Organs use OrganSurgeryProceduresComponent.
    /// Hands and feet omit DetachLimb; only ArmLeft, ArmRight, LegLeft, LegRight include it.
    /// When OrganOnlyPreset is set, this overrides the preset's organ steps when non-empty.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> OrganSteps { get; private set; } = new();

    /// <summary>
    /// When set, skin/tissue steps are empty and organ steps come from the preset (unless OrganSteps overrides).
    /// Used by Skeleton arms/legs and Cyber limbs.
    /// </summary>
    [DataField]
    public ProtoId<BodyPartSurgeryStepsPresetPrototype>? OrganOnlyPreset { get; private set; }

    /// <summary>
    /// Deprecated. Use SkinOpenSteps and SkinCloseSteps. Kept for migration.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> SkinSteps { get; private set; } = new();

    /// <summary>
    /// Deprecated. Use TissueOpenSteps and TissueCloseSteps. Kept for migration.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>> TissueSteps { get; private set; } = new();

    /// <summary>
    /// Resolves skin open/close steps, using step entries when present else legacy procedure lists.
    /// Returns empty when OrganOnlyPreset is set.
    /// </summary>
    public IReadOnlyList<string> GetSkinOpenStepIds(IPrototypeManager prototypes)
    {
        if (OrganOnlyPreset.HasValue)
            return Array.Empty<string>();
        if (SkinOpenStepEntries.Count > 0)
            return GetStepIdsFromEntries(SkinOpenStepEntries);
        if (SkinOpenSteps.Count > 0)
            return SkinOpenSteps.Select(s => s.ToString()).ToList();
        return DeriveOpenSteps(SkinSteps, prototypes);
    }

    /// <summary>
    /// Resolves skin close steps. Returns empty when OrganOnlyPreset is set.
    /// </summary>
    public IReadOnlyList<string> GetSkinCloseStepIds(IPrototypeManager prototypes)
    {
        if (OrganOnlyPreset.HasValue)
            return Array.Empty<string>();
        if (SkinCloseStepEntries.Count > 0)
            return GetStepIdsFromEntries(SkinCloseStepEntries);
        if (SkinCloseSteps.Count > 0)
            return SkinCloseSteps.Select(s => s.ToString()).ToList();
        return DeriveCloseSteps(SkinSteps, prototypes);
    }

    /// <summary>
    /// Resolves tissue open/close steps. Returns empty when OrganOnlyPreset is set.
    /// </summary>
    public IReadOnlyList<string> GetTissueOpenStepIds(IPrototypeManager prototypes)
    {
        if (OrganOnlyPreset.HasValue)
            return Array.Empty<string>();
        if (TissueOpenStepEntries.Count > 0)
            return GetStepIdsFromEntries(TissueOpenStepEntries);
        if (TissueOpenSteps.Count > 0)
            return TissueOpenSteps.Select(s => s.ToString()).ToList();
        return DeriveOpenSteps(TissueSteps, prototypes);
    }

    public IReadOnlyList<string> GetTissueCloseStepIds(IPrototypeManager prototypes)
    {
        if (OrganOnlyPreset.HasValue)
            return Array.Empty<string>();
        if (TissueCloseStepEntries.Count > 0)
            return GetStepIdsFromEntries(TissueCloseStepEntries);
        if (TissueCloseSteps.Count > 0)
            return TissueCloseSteps.Select(s => s.ToString()).ToList();
        return DeriveCloseSteps(TissueSteps, prototypes);
    }

    /// <summary>
    /// Returns effective organ step procedure IDs. When OrganOnlyPreset is set and OrganSteps is empty,
    /// uses the preset's organ steps; otherwise uses OrganSteps (explicit override).
    /// </summary>
    public IReadOnlyList<string> GetOrganStepIds(IPrototypeManager prototypes)
    {
        if (OrganSteps.Count > 0)
            return OrganSteps.Select(s => s.ToString()).ToList();
        if (OrganOnlyPreset.HasValue && prototypes.TryIndex(OrganOnlyPreset.Value, out BodyPartSurgeryStepsPresetPrototype? preset))
            return preset.OrganSteps.Select(s => s.ToString()).ToList();
        return Array.Empty<string>();
    }

    /// <summary>
    /// Generates step IDs from entries. Duplicate procedures get procedure_index suffix.
    /// </summary>
    private static List<string> GetStepIdsFromEntries(List<BodyPartSurgeryStepEntry> entries)
    {
        var procedureCounts = new Dictionary<string, int>();
        var result = new List<string>();
        foreach (var entry in entries)
        {
            var procId = entry.Procedure.ToString();
            var count = procedureCounts.GetValueOrDefault(procId, 0);
            procedureCounts[procId] = count + 1;
            result.Add(count == 0 ? procId : $"{procId}_{count}");
        }
        return result;
    }

    /// <summary>
    /// Returns the procedure ID for a step ID. Strips _index suffix for execution lookup.
    /// </summary>
    public static string GetProcedureForStep(string stepId)
    {
        var idx = stepId.LastIndexOf('_');
        if (idx > 0 && idx < stepId.Length - 1 && int.TryParse(stepId.AsSpan(idx + 1), out _))
            return stepId[..idx];
        return stepId;
    }

    /// <summary>
    /// Returns skin open step entries with step IDs, procedure IDs, and display names.
    /// </summary>
    public IReadOnlyList<(string StepId, string ProcedureId, LocId? DisplayName)> GetSkinOpenStepEntries(IPrototypeManager prototypes)
    {
        if (OrganOnlyPreset.HasValue)
            return Array.Empty<(string, string, LocId?)>();
        return GetStepEntries(SkinOpenStepEntries, SkinOpenSteps, SkinSteps, prototypes, forOpen: true);
    }

    /// <summary>
    /// Returns skin close step entries.
    /// </summary>
    public IReadOnlyList<(string StepId, string ProcedureId, LocId? DisplayName)> GetSkinCloseStepEntries(IPrototypeManager prototypes)
    {
        if (OrganOnlyPreset.HasValue)
            return Array.Empty<(string, string, LocId?)>();
        return GetStepEntries(SkinCloseStepEntries, SkinCloseSteps, SkinSteps, prototypes, forOpen: false);
    }

    /// <summary>
    /// Returns tissue open step entries.
    /// </summary>
    public IReadOnlyList<(string StepId, string ProcedureId, LocId? DisplayName)> GetTissueOpenStepEntries(IPrototypeManager prototypes)
    {
        if (OrganOnlyPreset.HasValue)
            return Array.Empty<(string, string, LocId?)>();
        return GetStepEntries(TissueOpenStepEntries, TissueOpenSteps, TissueSteps, prototypes, forOpen: true);
    }

    /// <summary>
    /// Returns tissue close step entries.
    /// </summary>
    public IReadOnlyList<(string StepId, string ProcedureId, LocId? DisplayName)> GetTissueCloseStepEntries(IPrototypeManager prototypes)
    {
        if (OrganOnlyPreset.HasValue)
            return Array.Empty<(string, string, LocId?)>();
        return GetStepEntries(TissueCloseStepEntries, TissueCloseSteps, TissueSteps, prototypes, forOpen: false);
    }

    private IReadOnlyList<(string StepId, string ProcedureId, LocId? DisplayName)> GetStepEntries(
        List<BodyPartSurgeryStepEntry> entries,
        List<ProtoId<SurgeryProcedurePrototype>> legacySteps,
        List<ProtoId<SurgeryProcedurePrototype>> deprecatedSteps,
        IPrototypeManager prototypes,
        bool forOpen)
    {
        if (entries.Count > 0)
        {
            var procedureCounts = new Dictionary<string, int>();
            var result = new List<(string, string, LocId?)>();
            foreach (var entry in entries)
            {
                var procId = entry.Procedure.ToString();
                var count = procedureCounts.GetValueOrDefault(procId, 0);
                procedureCounts[procId] = count + 1;
                var stepId = count == 0 ? procId : $"{procId}_{count}";
                var displayName = entry.Name ?? (prototypes.TryIndex(entry.Procedure, out SurgeryProcedurePrototype? proc) ? proc.Name : null);
                result.Add((stepId, procId, displayName));
            }
            return result;
        }
        var legacyList = legacySteps.Count > 0
            ? legacySteps.Select(s => s.ToString()).ToList()
            : (forOpen ? DeriveOpenSteps(deprecatedSteps, prototypes) : DeriveCloseSteps(deprecatedSteps, prototypes));
        return legacyList.Select(s => (s, s, prototypes.TryIndex<SurgeryProcedurePrototype>(s, out var p) ? p.Name : (LocId?)null)).ToList();
    }

    private static List<string> DeriveOpenSteps(List<ProtoId<SurgeryProcedurePrototype>> steps, IPrototypeManager prototypes)
    {
        var result = new List<string>();
        foreach (var stepId in steps)
        {
            if (prototypes.TryIndex(stepId, out SurgeryProcedurePrototype? step) && step.HealAmount is { } h && !h.Empty)
                break;
            result.Add(stepId.ToString());
        }
        return result;
    }

    private static List<string> DeriveCloseSteps(List<ProtoId<SurgeryProcedurePrototype>> steps, IPrototypeManager prototypes)
    {
        var result = new List<string>();
        var inClose = false;
        foreach (var stepId in steps)
        {
            if (prototypes.TryIndex(stepId, out SurgeryProcedurePrototype? step) && step.HealAmount is { } h && !h.Empty)
                inClose = true;
            if (inClose)
                result.Add(stepId.ToString());
        }
        return result;
    }
}
