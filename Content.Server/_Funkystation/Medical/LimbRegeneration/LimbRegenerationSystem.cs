using System.Linq;
using Content.Server.Medical.LimbRegeneration.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Containers;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Medical.LimbRegeneration;

public sealed class LimbRegenerationSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<SpeciesPrototype> SlimePerson = "SlimePerson";

    private static readonly ProtoId<OrganCategoryPrototype>[] LimbAndHeadCategories =
    [
        "ArmLeft",
        "ArmRight",
        "LegLeft",
        "LegRight",
        "Head"
    ];

    public override void Initialize()
    {
        base.Initialize();
    }

    /// <summary>
    /// Called when an organ is removed from a body. HealthAnalyzerSystem subscribes to OrganRemovedFromEvent
    /// and invokes this to avoid duplicate subscriptions.
    /// </summary>
    public void OnOrganRemovedFrom(Entity<BodyComponent> ent, ref OrganRemovedFromEvent args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (!TryComp<HumanoidProfileComponent>(ent, out var humanoidProfile) || humanoidProfile.Species != SlimePerson)
            return;

        if (!TryComp<OrganComponent>(args.Organ, out var organ) || organ.Category is not { } category)
            return;

        if (!LimbAndHeadCategories.Contains(category))
            return;

        var regen = EnsureComp<SlimeLimbRegenerationComponent>(ent);
        regen.PendingRegenerations.Add(new SlimeLimbRegenerationEntry(category, _timing.CurTime));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SlimeLimbRegenerationComponent, BodyComponent, HumanoidProfileComponent>();

        while (query.MoveNext(out var uid, out var regen, out var bodyComp, out var humanoidProfile))
        {
            if (regen.PendingRegenerations.Count == 0)
                continue;

            var toRemove = new List<int>();
            for (var i = 0; i < regen.PendingRegenerations.Count; i++)
            {
                var entry = regen.PendingRegenerations[i];
                if (curTime - entry.RegenerationStartTime < regen.RegenerationDelay)
                    continue;

                if (RestoreSingleLimb(uid, humanoidProfile.Species, entry.Category, bodyComp))
                    toRemove.Add(i);
            }

            for (var i = toRemove.Count - 1; i >= 0; i--)
            {
                regen.PendingRegenerations.RemoveAt(toRemove[i]);
            }

            if (regen.PendingRegenerations.Count == 0)
                RemComp<SlimeLimbRegenerationComponent>(uid);
        }
    }

    public bool RestoreSingleLimb(EntityUid body, ProtoId<SpeciesPrototype> speciesId, ProtoId<OrganCategoryPrototype> category, BodyComponent? bodyComp = null)
    {
        if (!Resolve(body, ref bodyComp) || bodyComp.Organs == null)
            return false;

        if (!_prototypes.TryIndex(speciesId, out SpeciesPrototype? species) || species.LimbOrganPrototypes == null)
            return false;

        if (!species.LimbOrganPrototypes.TryGetValue(category, out var organProto))
            return false;

        if (_body.GetAllOrgans(body).Any(o => TryComp<OrganComponent>(o, out var oComp) && oComp.Category == category))
            return false;

        var coords = Transform(body).Coordinates;
        var limb = Spawn(organProto, coords);
        return _container.Insert(limb, bodyComp.Organs);
    }

    public void RestoreAllLimbs(EntityUid body)
    {
        if (!TryComp<HumanoidProfileComponent>(body, out var humanoidProfile))
            return;

        foreach (var category in LimbAndHeadCategories)
        {
            RestoreSingleLimb(body, humanoidProfile.Species, category);
        }
    }

    private static readonly ProtoId<OrganCategoryPrototype>[] LimbCategories =
    [
        "ArmLeft",
        "ArmRight",
        "LegLeft",
        "LegRight"
    ];

    private static readonly (ProtoId<OrganCategoryPrototype> Category, EntProtoId Proto)[] CyberLimbPrototypes =
    [
        ("ArmLeft", "OrganCyberArmLeftPreFilled"),
        ("ArmRight", "OrganCyberArmRightPreFilled"),
        ("LegLeft", "OrganCyberLegLeftPreFilled"),
        ("LegRight", "OrganCyberLegRightPreFilled"),
    ];

    /// <summary>
    /// Removes all arms and legs, then replaces them with pre-filled cyber limbs.
    /// Used by the admin "Replace with cybernetics" smite.
    /// </summary>
    public void ReplaceAllLimbsWithCybernetics(EntityUid body)
    {
        BodyComponent? bodyComp = null;
        if (!Resolve(body, ref bodyComp) || bodyComp.Organs is not { } organsContainer)
            return;

        var coords = Transform(body).Coordinates;
        var limbCategories = new HashSet<ProtoId<OrganCategoryPrototype>>(LimbCategories);

        // Remove existing limbs (only organic - skip if already cyber to avoid double-replacement crash)
        // ToList() required: OrganRemoveRequestEvent modifies the organs container during iteration
        foreach (var organ in _body.GetAllOrgans(body).ToList())
        {
            if (!TryComp(organ, out OrganComponent? organComp) || organComp.Category is not { } category || !limbCategories.Contains(category))
                continue;

            if (HasComp<CyberLimbComponent>(organ))
                continue;

            var removeEv = new OrganRemoveRequestEvent(organ) { Destination = coords };
            RaiseLocalEvent(organ, ref removeEv);
        }

        // Insert pre-filled cyber limbs
        foreach (var (category, proto) in CyberLimbPrototypes)
        {
            if (_body.GetAllOrgans(body).Any(o => TryComp<OrganComponent>(o, out var oComp) && oComp.Category == category))
                continue;

            var limb = Spawn(proto, coords);
            _container.Insert(limb, organsContainer);
        }
    }
}
