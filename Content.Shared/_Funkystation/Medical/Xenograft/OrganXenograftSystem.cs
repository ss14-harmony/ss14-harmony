using System;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Metabolism;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Medical.Xenograft;

/// <summary>
/// Applies xenograft metabolizer retagging, effectiveness scaling, and integrity penalties when organs move between bodies.
/// </summary>
public sealed class OrganXenograftSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Snapshot of prototype metabolizer types before any host-specific mutation (for explant restore).
    /// </summary>
    private readonly Dictionary<EntityUid, HashSet<ProtoId<MetabolizerTypePrototype>>> _metabolizerSnapshots = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganXenograftComponent, MapInitEvent>(OnXenograftMapInit);
        SubscribeLocalEvent<OrganXenograftComponent, EntityTerminatingEvent>(OnXenograftTerminating);
    }

    /// <summary>
    /// Called from <see cref="Content.Shared.Cybernetics.Systems.CyberOrganMetabolismModifierSystem"/> — cannot subscribe to
    /// <see cref="GetOrganMetabolismScaleModifierEvent"/> on <see cref="BodyComponent"/> twice (duplicate subscription).
    /// </summary>
    public void ApplyForeignHostMetabolismScale(EntityUid body, ref GetOrganMetabolismScaleModifierEvent args)
    {
        if (!TryComp<OrganXenograftComponent>(args.Organ, out var xenograft))
            return;

        if (!ResolveRecipientSpecies(body, out var recipientSpecies))
            return;

        var quality = ComputeQuality(xenograft, recipientSpecies);
        // Native match yields 1.0 — no change. Foreign hosts can use below or above 1 (e.g. bonus organs).
        if (MathF.Abs(quality - 1f) < 1e-4f)
            return;

        args.Scale *= quality;
    }

    private void OnXenograftTerminating(EntityUid uid, OrganXenograftComponent _, ref EntityTerminatingEvent args)
    {
        _metabolizerSnapshots.Remove(uid);
    }

    private void OnXenograftMapInit(Entity<OrganXenograftComponent> ent, ref MapInitEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (!TryComp<MetabolizerComponent>(ent, out var meta) || meta.MetabolizerTypes == null)
        {
            _metabolizerSnapshots[ent.Owner] = new HashSet<ProtoId<MetabolizerTypePrototype>>();
            return;
        }

        _metabolizerSnapshots[ent.Owner] = new HashSet<ProtoId<MetabolizerTypePrototype>>(meta.MetabolizerTypes);
    }

    /// <summary>
    /// Invoked from <see cref="Content.Shared.Medical.Integrity.IntegrityUsageSystem"/> — cannot subscribe to <see cref="OrganGotInsertedEvent"/>
    /// on <see cref="OrganComponent"/> twice.
    /// </summary>
    public void HandleOrganInserted(Entity<OrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (!TryComp<OrganXenograftComponent>(ent, out var xenograft))
            return;

        if (!ResolveRecipientSpecies(args.Target, out var recipientSpecies))
        {
            ClearXenograftIntegrityPenalty(ent.Owner);
            return;
        }

        var quality = ComputeQuality(xenograft, recipientSpecies);
        ApplyMetabolizerTagsForHost(ent.Owner, xenograft, recipientSpecies);

        var xPenalty = quality >= 1f - 1e-4f
            ? 0
            : Math.Min(6, (int)Math.Ceiling((1f - quality) * 6f));

        var penaltyComp = EnsureComp<IntegrityPenaltyComponent>(ent.Owner);
        penaltyComp.XenograftPenalty = xPenalty;
        Dirty(ent.Owner, penaltyComp);
    }

    /// <inheritdoc cref="HandleOrganInserted"/>
    public void HandleOrganRemoved(Entity<OrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (!HasComp<OrganXenograftComponent>(ent))
            return;

        RestoreMetabolizerTypes(ent.Owner);
        ClearXenograftIntegrityPenalty(ent.Owner);
    }

    private void ClearXenograftIntegrityPenalty(EntityUid organ)
    {
        if (!TryComp<IntegrityPenaltyComponent>(organ, out var penaltyComp) || penaltyComp.XenograftPenalty == 0)
            return;

        penaltyComp.XenograftPenalty = 0;
        if (penaltyComp.Penalty <= 0)
            RemCompDeferred<IntegrityPenaltyComponent>(organ);
        else
            Dirty(organ, penaltyComp);
    }

    private void RestoreMetabolizerTypes(EntityUid organ)
    {
        if (!_metabolizerSnapshots.TryGetValue(organ, out var snapshot))
            return;

        if (!TryComp<MetabolizerComponent>(organ, out var meta))
            return;

        meta.MetabolizerTypes = new HashSet<ProtoId<MetabolizerTypePrototype>>(snapshot);
        Dirty(organ, meta);
    }

    private void ApplyMetabolizerTagsForHost(EntityUid organ, OrganXenograftComponent xenograft, ProtoId<SpeciesPrototype> recipientSpecies)
    {
        if (!TryComp<MetabolizerComponent>(organ, out var meta))
            return;

        var recipientType = ResolveCanonicalMetabolizerType(recipientSpecies);
        if (recipientType == null)
            return;

        // Restore from snapshot first so re-insert cycles don't stack types.
        RestoreMetabolizerTypes(organ);

        meta.MetabolizerTypes ??= new HashSet<ProtoId<MetabolizerTypePrototype>>();
        if (!meta.MetabolizerTypes.Contains(recipientType.Value))
            meta.MetabolizerTypes.Add(recipientType.Value);

        Dirty(organ, meta);
    }

    /// <summary>
    /// Resolves donor species id for slime implant rules (organ in hand).
    /// </summary>
    public bool TryGetNativeSpecies(EntityUid organ, out ProtoId<SpeciesPrototype> species)
    {
        species = default;
        if (TryComp<OrganXenograftComponent>(organ, out var xeno))
        {
            species = xeno.NativeSpecies;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Limb donor species for AttachLimb (uses surgery tagging).
    /// </summary>
    public bool TryGetLimbDonorSpecies(EntityUid limb, out ProtoId<SpeciesPrototype> species)
    {
        species = default;
        if (TryComp<SurgeryBodyPartComponent>(limb, out var tag))
        {
            species = tag.SpeciesId;
            return true;
        }

        return false;
    }

    private float ComputeQuality(OrganXenograftComponent xenograft, ProtoId<SpeciesPrototype> recipientSpecies)
    {
        if (xenograft.NativeSpecies == recipientSpecies)
            return 1f;

        if (xenograft.ForeignQualityOverrides.TryGetValue(recipientSpecies, out var q))
            return q;

        return xenograft.ForeignQualityDefault;
    }

    private bool ResolveRecipientSpecies(EntityUid body, out ProtoId<SpeciesPrototype> speciesId)
    {
        speciesId = default;
        if (TryComp<HumanoidProfileComponent>(body, out var humanoid))
        {
            speciesId = humanoid.Species;
            return true;
        }

        if (TryComp<CreatureDonorSpeciesComponent>(body, out var creature))
        {
            speciesId = creature.Species;
            return true;
        }

        return false;
    }

    private ProtoId<MetabolizerTypePrototype>? ResolveCanonicalMetabolizerType(ProtoId<SpeciesPrototype> speciesId)
    {
        // Mirrors Body/Species/* metabolizerTypes on appearance prototypes (canonical mapping).
        var id = speciesId.Id;
        return id switch
        {
            "Human" => new ProtoId<MetabolizerTypePrototype>("Human"),
            "Dwarf" => new ProtoId<MetabolizerTypePrototype>("Dwarf"),
            "SlimePerson" => new ProtoId<MetabolizerTypePrototype>("Slime"),
            "Vox" => new ProtoId<MetabolizerTypePrototype>("Vox"),
            "Reptilian" => new ProtoId<MetabolizerTypePrototype>("Animal"),
            "Moth" => new ProtoId<MetabolizerTypePrototype>("Moth"),
            "Arachnid" => new ProtoId<MetabolizerTypePrototype>("Arachnid"),
            "Diona" => new ProtoId<MetabolizerTypePrototype>("Plant"),
            "Vulpkanin" => new ProtoId<MetabolizerTypePrototype>("Animal"),
            "Gingerbread" => new ProtoId<MetabolizerTypePrototype>("Human"),
            "Skeleton" => new ProtoId<MetabolizerTypePrototype>("Human"),
            // Donor-only creature species (animal-type metabolism inside those mobs).
            "Monkey" => new ProtoId<MetabolizerTypePrototype>("Animal"),
            "SpaceCarp" => new ProtoId<MetabolizerTypePrototype>("Animal"),
            "SpaceDragon" => new ProtoId<MetabolizerTypePrototype>("Dragon"),
            _ => (ProtoId<MetabolizerTypePrototype>?)null
        };
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _metabolizerSnapshots.Clear();
    }
}
