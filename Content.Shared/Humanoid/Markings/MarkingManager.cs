using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Body;
using Content.Shared.Containers; // Funky - CyberMed
using Content.Shared.EntityTable; // Funky - CyberMed
using Content.Shared.EntityTable.EntitySelectors; // Funky - CyberMed
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameObjects; // Funky - CyberMed
using Robust.Shared.Log; // Funky - CyberMed
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid.Markings;

/// <summary>
/// Manager responsible for sharing the logic of markings between in-simulation bodies and out-of-simulation profile editing
/// </summary>
public sealed partial class MarkingManager
{
    private static readonly ISawmill Sawmill = Logger.GetSawmill("marking"); // Funky - CyberMed

    [Dependency] private IComponentFactory _component = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IEntityManager _entityManager = default!; // Funky - CyberMed

    private FrozenDictionary<HumanoidVisualLayers, FrozenDictionary<string, MarkingPrototype>> _categorizedMarkings = default!;
    private FrozenDictionary<string, MarkingPrototype> _markings = default!;

    private readonly Dictionary<string, Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId<OrganComponent>>> _organMapCache = new(); // Funky - CyberMed
    
    public void Initialize()
    {
        _prototype.PrototypesReloaded += OnPrototypeReload;
        CachePrototypes();
    }

    private void CachePrototypes()
    {
        var markingDict = new Dictionary<HumanoidVisualLayers, Dictionary<string, MarkingPrototype>>();

        foreach (var category in Enum.GetValues<HumanoidVisualLayers>())
        {
            markingDict.Add(category, new());
        }

        foreach (var prototype in _prototype.EnumeratePrototypes<MarkingPrototype>())
        {
            try
            {
                markingDict[prototype.BodyPart].Add(prototype.ID, prototype);
            }
            catch (Exception e)
            {
                throw new Exception($"failed to process {prototype.ID}", e);
            }
        }

        _markings = _prototype.EnumeratePrototypes<MarkingPrototype>().ToFrozenDictionary(x => x.ID);
        _categorizedMarkings = markingDict.ToFrozenDictionary(
            x => x.Key,
            x => x.Value.ToFrozenDictionary());
    }

    public FrozenDictionary<string, MarkingPrototype> MarkingsByLayer(HumanoidVisualLayers category)
    {
        // all marking categories are guaranteed to have a dict entry
        return _categorizedMarkings[category];
    }

    /// <summary>
    ///     Markings by category, species and sex.
    /// </summary>
    /// <remarks>
    ///     This is done per category, as enumerating over every single marking by group isn't useful.
    ///     Please make a pull request if you find a use case for that behavior.
    /// </remarks>
    /// <returns></returns>
    public IReadOnlyDictionary<string, MarkingPrototype> MarkingsByLayerAndGroupAndSex(HumanoidVisualLayers layer,
        ProtoId<MarkingsGroupPrototype> group,
        Sex sex)
    {
        var groupProto = _prototype.Index(group);
        var whitelisted = groupProto.Limits.GetValueOrDefault(layer)?.OnlyGroupWhitelisted ?? groupProto.OnlyGroupWhitelisted;
        var res = new Dictionary<string, MarkingPrototype>();

        foreach (var (key, marking) in MarkingsByLayer(layer))
        {
            if (!CanBeApplied(groupProto, sex, marking, whitelisted))
                continue;

            res.Add(key, marking);
        }

        return res;
    }

    public bool TryGetMarking(Marking marking, [NotNullWhen(true)] out MarkingPrototype? markingResult)
    {
        return _markings.TryGetValue(marking.MarkingId, out markingResult);
    }

    private void OnPrototypeReload(PrototypesReloadedEventArgs args)
    {
        _organMapCache.Clear();
        if (args.WasModified<MarkingPrototype>())
            CachePrototypes();
    }


    public bool CanBeApplied(ProtoId<MarkingsGroupPrototype> group, Sex sex, MarkingPrototype prototype)
    {
        var groupProto = _prototype.Index(group);
        var whitelisted = groupProto.Limits.GetValueOrDefault(prototype.BodyPart)?.OnlyGroupWhitelisted ?? groupProto.OnlyGroupWhitelisted;

        return CanBeApplied(groupProto, sex, prototype, whitelisted);
    }

    private bool CanBeApplied(MarkingsGroupPrototype group, Sex sex, MarkingPrototype prototype, bool whitelisted)
    {
        if (prototype.GroupWhitelist == null)
        {
            if (whitelisted)
                return false;
        }
        else
        {
            if (!prototype.GroupWhitelist.Contains(group))
                return false;
        }

        return prototype.SexRestriction == null || prototype.SexRestriction == sex;
    }

    /// <summary>
    /// Ensures that the <see cref="markingSets"/> have a valid amount of colors
    /// </summary>
    public void EnsureValidColors(Dictionary<HumanoidVisualLayers, List<Marking>> markingSets)
    {
        foreach (var markings in markingSets.Values)
        {
            for (var i = markings.Count - 1; i >= 0; i--)
            {
                if (!TryGetMarking(markings[i], out var marking))
                {
                    markings.RemoveAt(i);
                    continue;
                }

                if (marking.Sprites.Count != markings[i].MarkingColors.Count)
                {
                    markings[i] = new Marking(marking.ID, marking.Sprites.Count);
                }
            }
        }
    }

    /// <summary>
    /// Ensures that the <see cref="markingSets"/> are valid per the constraints on <see cref="group"/> and <see cref="sex"/>
    /// </summary>
    public void EnsureValidGroupAndSex(Dictionary<HumanoidVisualLayers, List<Marking>> markingSets, ProtoId<MarkingsGroupPrototype> group, Sex sex)
    {
        foreach (var markings in markingSets.Values)
        {
            for (var i = markings.Count - 1; i >= 0; i--)
            {
                if (!TryGetMarking(markings[i], out var marking) || !CanBeApplied(group, sex, marking))
                    markings.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Ensures that the <see cref="markingSets"/> only belong to the <see cref="layers"/>
    /// </summary>
    public void EnsureValidLayers(Dictionary<HumanoidVisualLayers, List<Marking>> markingSets, HashSet<HumanoidVisualLayers> layers)
    {
        foreach (var markings in markingSets.Values)
        {
            for (var i = markings.Count - 1; i >= 0; i--)
            {
                if (!TryGetMarking(markings[i], out var marking) || !layers.Contains(marking.BodyPart))
                    markings.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Ensures the list of <see cref="markingSets"/> is valid per the limits of the <see cref="group"/>
    /// </summary>
    public void EnsureValidLimits(Dictionary<HumanoidVisualLayers, List<Marking>> markingSets, ProtoId<MarkingsGroupPrototype> group, HashSet<HumanoidVisualLayers> layers, Color? skinColor, Color? eyeColor)
    {
        var groupProto = _prototype.Index(group);
        var counts = new Dictionary<HumanoidVisualLayers, int>();

        foreach (var (_, markings) in markingSets)
        {
            for (var i = markings.Count - 1; i >= 0; i--)
            {
                if (!TryGetMarking(markings[i], out var marking))
                {
                    markings.RemoveAt(i);
                    continue;
                }

                if (!groupProto.Limits.TryGetValue(marking.BodyPart, out var limit))
                    continue;

                var count = counts.GetValueOrDefault(marking.BodyPart);
                if (count >= limit.Limit)
                {
                    markings.RemoveAt(i);
                    continue;
                }

                counts[marking.BodyPart] = counts.GetValueOrDefault(marking.BodyPart) + 1;
            }
        }

        foreach (var layer in layers)
        {
            if (!groupProto.Limits.TryGetValue(layer, out var layerLimit))
                continue;

            var layerCounts = counts.GetValueOrDefault(layer);
            if (layerCounts > 0 || !layerLimit.Required)
                continue;

            foreach (var marking in layerLimit.Default)
            {
                if (!_markings.TryGetValue(marking, out var markingProto))
                    continue;

                markingSets[layer] = markingSets.GetValueOrDefault(layer) ?? [];
                var colors = MarkingColoring.GetMarkingLayerColors(markingProto, skinColor, eyeColor, markingSets[layer]);
                markingSets[layer].Add(new(marking, colors));
            }
        }
    }

    public Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId<OrganComponent>> GetOrgans(ProtoId<SpeciesPrototype> species)
    {
        var id = species.Id;
        if (_organMapCache.TryGetValue(id, out var cached))
            return new Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId<OrganComponent>>(cached);

        var built = BuildOrgansUncached(species);
        _organMapCache[id] = built;
        return new Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId<OrganComponent>>(built);
    }

    /// <summary>
    /// Builds the organ prototype map for marking metadata. Uses <see cref="InitialBodyComponent"/> only
    /// when present; otherwise merges <c>body_organs</c> <see cref="EntityTableContainerFillComponent"/> spawns
    /// with explicit <see cref="SpeciesPrototype.LimbOrganPrototypes"/> overrides.
    /// </summary>
    private Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId<OrganComponent>> BuildOrgansUncached(
        ProtoId<SpeciesPrototype> species)
    {
        var speciesPrototype = _prototype.Index(species);
        if (!_prototype.TryIndex(speciesPrototype.DollPrototype, out var appearancePrototype))
            return new Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId<OrganComponent>>();

        if (appearancePrototype.TryGetComponent<InitialBodyComponent>(out var initialBody, _component))
            return new Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId<OrganComponent>>(initialBody.Organs);

        var merged = new Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId<OrganComponent>>();

        if (TryBuildOrgansFromBodyOrgansTable(appearancePrototype, out var tableMap))
        {
            foreach (var kvp in tableMap)
                merged[kvp.Key] = kvp.Value;
        }

        if (speciesPrototype.LimbOrganPrototypes != null)
        {
            foreach (var (category, protoId) in speciesPrototype.LimbOrganPrototypes)
                merged[category] = new EntProtoId<OrganComponent>(protoId.Id);
        }

        return merged;
    }

    /// <summary>
    /// Lists organ entity prototypes from the appearance doll's <c>body_organs</c> fill table,
    /// recursively following every discovered organ's own EntityTableContainerFillComponent
    /// so nested part-of-a-part organs (e.g. hand organs inside arm organs, foot organs inside leg
    /// organs, eyes/brain inside head organs) are also included.
    /// Randomized tables may not yield a stable full set; those species should use <see cref="InitialBodyComponent"/> or explicit limb maps.
    /// </summary>
    private bool TryBuildOrgansFromBodyOrgansTable(
        EntityPrototype appearancePrototype,
        out Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId<OrganComponent>> map)
    {
        map = new Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId<OrganComponent>>();

        if (!appearancePrototype.TryGetComponent<EntityTableContainerFillComponent>(out var fill, _component))
            return false;

        if (!fill.Containers.TryGetValue(BodyComponent.ContainerID, out var rootSelector)) // Funky - CyberMed
            return false;

        var ctx = new EntityTableContext();
        var warnedDuplicate = new HashSet<string>();
        var visited = new HashSet<string>(); // Funky - CyberMed

        // Funky - CyberMed
        var pending = new Queue<EntityTableSelector>();
        pending.Enqueue(rootSelector);

        while (pending.TryDequeue(out var selector))
        {
            foreach (var (spawn, _) in selector.ListSpawns(_entityManager, _prototype, ctx))
            {
                if (!visited.Add(spawn.Id))
                    continue;

                if (!_prototype.TryIndex(spawn, out var entProto))
                    continue;

                if (entProto.TryGetComponent<OrganComponent>(out var organComp, _component)
                    && organComp.Category is { } category)
                {
                    var protoId = new EntProtoId<OrganComponent>(spawn.Id);
                    if (map.TryGetValue(category, out var existing) && existing != protoId)
                    {
                        if (warnedDuplicate.Add(category.Id))
                        {
                            Sawmill.Warning(
                                "Multiple body_organs prototypes map to organ category {0} on appearance {1}; keeping first match.",
                                category.Id,
                                appearancePrototype.ID);
                        }
                    }
                    else
                    {
                        map[category] = protoId;
                    }
                }

                // Recurse into this organ's own fill containers so nested organs
                // (e.g. hands inside arms, feet inside legs) are discovered too.
                if (entProto.TryGetComponent<EntityTableContainerFillComponent>(out var nestedFill, _component))
                {
                    foreach (var nestedSelector in nestedFill.Containers.Values)
                        pending.Enqueue(nestedSelector);
                }
            }
        }

        return true;
    }

    public Dictionary<ProtoId<OrganCategoryPrototype>, OrganMarkingData> GetMarkingData(ProtoId<SpeciesPrototype> species)
    {
        var ret = new Dictionary<ProtoId<OrganCategoryPrototype>, OrganMarkingData>();

        foreach (var (organ, proto) in GetOrgans(species))
        {
            if (!TryGetMarkingData(proto, out var organData))
                continue;

            ret[organ] = organData.Value;
        }

        return ret;
    }

    public Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData> GetProfileData(ProtoId<SpeciesPrototype> species,
        Sex sex,
        Color skinColor,
        Color eyeColor)
    {
        var ret = new Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>();

        foreach (var organ in GetOrgans(species).Keys)
        {
            ret[organ] = new()
            {
                Sex = sex,
                EyeColor = eyeColor,
                SkinColor = skinColor,
            };
        }

        return ret;
    }

    public bool TryGetMarkingData(EntProtoId organ, [NotNullWhen(true)] out OrganMarkingData? organData)
    {
        organData = null;

        if (!_prototype.TryIndex(organ, out var organProto))
            return false;

        if (!organProto.TryGetComponent<VisualOrganMarkingsComponent>(out var comp, _component))
            return false;

        organData = comp.MarkingData;

        return true;
    }

    public Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> ConvertMarkings(List<Marking> markings,
        ProtoId<SpeciesPrototype> species)
    {
        var ret = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>();

        var data = GetMarkingData(species);
        var layersToOrgans = data.SelectMany(kvp => kvp.Value.Layers.Select(layer => (layer, kvp.Key))).ToDictionary(pair => pair.layer, pair => pair.Key);

        foreach (var marking in markings)
        {
            if (!_prototype.TryIndex<MarkingPrototype>(marking.MarkingId, out var markingProto))
                continue;

            if (!layersToOrgans.TryGetValue(markingProto.BodyPart, out var organ))
                continue;

            var organDict = ret.GetValueOrDefault(organ) ?? [];
            ret[organ] = organDict;
            var markingList = organDict.GetValueOrDefault(markingProto.BodyPart) ?? [];
            organDict[markingProto.BodyPart] = markingList;

            markingList.Add(marking);
        }

        return ret;
    }

    /// <summary>
    /// Recursively compares two markings dictionaries for equality.
    /// </summary>
    /// <param name="a">The first markings dictionary.</param>
    /// <param name="b">The second markings dictionary.</param>
    /// <returns>Whether the dictionaries are equivalent.</returns>
    public static bool MarkingsAreEqual(Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> a,
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> b)
    {
        if (a.Count != b.Count)
            return false;

        foreach (var (organ, aDictionary) in a)
        {
            if (!b.TryGetValue(organ, out var bDictionary))
                return false;

            if (aDictionary.Count != bDictionary.Count)
                return false;

            foreach (var (layer, aMarkings) in aDictionary)
            {
                if (!bDictionary.TryGetValue(layer, out var bMarkings))
                    return false;

                if (!aMarkings.SequenceEqual(bMarkings))
                    return false;
            }
        }

        return true;
    }
}
