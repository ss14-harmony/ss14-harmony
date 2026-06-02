using System.Collections.Generic; // Funky - CyberMed
using System.Linq; // Funky - CyberMed
using Content.Shared.Body.Components; // Funky - CyberMed
using Content.Shared.Body.Events; // Funky - CyberMed
using Content.Shared.DragDrop;
using Content.Shared.Medical.Surgery.Components; // Funky - CyberMed
using Robust.Shared.Containers;

namespace Content.Shared.Body;

public sealed partial class BodySystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    [Dependency] private EntityQuery<BodyComponent> _bodyQuery = default!;
    [Dependency] private EntityQuery<OrganComponent> _organQuery = default!;
    [Dependency] private EntityQuery<BodyPartComponent> _bodyPartQuery = default!; // Funky - CyberMed

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, ComponentInit>(OnBodyInit);
        SubscribeLocalEvent<BodyComponent, ComponentShutdown>(OnBodyShutdown);

        SubscribeLocalEvent<BodyPartComponent, ComponentInit>(OnBodyPartInit); // Funky - CyberMed
        SubscribeLocalEvent<BodyPartComponent, ComponentShutdown>(OnBodyPartShutdown); // Funky - CyberMed

        SubscribeLocalEvent<BodyComponent, CanDragEvent>(OnCanDrag); // Funky - CyberMed

        SubscribeLocalEvent<BodyComponent, EntInsertedIntoContainerMessage>(OnBodyEntInserted); // Funky - CyberMed
        SubscribeLocalEvent<BodyComponent, EntRemovedFromContainerMessage>(OnBodyEntRemoved); // Funky - CyberMed

        // Funky - CyberMed: Start
        SubscribeLocalEvent<BodyPartComponent, EntInsertedIntoContainerMessage>(OnBodyPartEntInserted);
        SubscribeLocalEvent<BodyPartComponent, EntRemovedFromContainerMessage>(OnBodyPartEntRemoved);

        SubscribeLocalEvent<BodyComponent, BodyPartQueryEvent>(OnBodyPartQuery);
        SubscribeLocalEvent<BodyComponent, BodyPartQueryByTypeEvent>(OnBodyPartQueryByType);

        _bodyQuery = GetEntityQuery<BodyComponent>();
        _organQuery = GetEntityQuery<OrganComponent>();
        _bodyPartQuery = GetEntityQuery<BodyPartComponent>();

        InitializeRelay();
    }
    // Funkystation: OnBodyPartInit added
    private void OnBodyPartInit(Entity<BodyPartComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Organs =
            _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        EnsureComp<SurgeryLayerComponent>(ent);
    }

    private void OnBodyPartShutdown(Entity<BodyPartComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Organs is { } organs)
            _container.ShutdownContainer(organs);
    }
    // Funky - CyberMed: End

    private void OnBodyInit(Entity<BodyComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Organs =
            _container.EnsureContainer<Container>(ent, BodyComponent.ContainerID);
    }

    private void OnBodyShutdown(Entity<BodyComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Organs is { } organs)
            _container.ShutdownContainer(organs);
    }

    private void OnBodyEntInserted(Entity<BodyComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        if (!_organQuery.TryComp(args.Entity, out var organ))
            return;

        // Funky - CyberMed: Start
        // Set BodyPart.Body when a body part is inserted, and propagate to any organs already in it
        if (_bodyPartQuery.TryComp(args.Entity, out var bodyPart))
        {
            bodyPart.Body = ent;
            Dirty(args.Entity, bodyPart);

            // Re-attached limbs retain their SurgeryLayerComponent state (performed skin/tissue/organ steps).
            // The detached limb entity keeps its surgery progress, so no modification is needed on re-insert.

            // Organs may have been inserted before this part was in the body (e.g. during MapInit).
            // Catch up: set Organ.Body and raise OrganGotInsertedEvent for each.
            if (bodyPart.Organs != null)
            {
                foreach (var child in bodyPart.Organs.ContainedEntities)
                {
                    if (_organQuery.TryComp(child, out var childOrgan))
                    {
                        var bodyEv = new OrganInsertedIntoEvent(child);
                        RaiseLocalEvent(ent, ref bodyEv);
                        var insertEv = new OrganGotInsertedEvent(ent);
                        RaiseLocalEvent(child, ref insertEv);
                        if (childOrgan.Body != ent)
                        {
                            childOrgan.Body = ent;
                            Dirty(child, childOrgan);
                        }
                    }
                }
            }
        }
        // Funky - CyberMed: End

        var body = new OrganInsertedIntoEvent(args.Entity);
        RaiseLocalEvent(ent, ref body);

        var ev = new OrganGotInsertedEvent(ent);
        RaiseLocalEvent(args.Entity, ref ev);

        if (organ.Body != ent)
        {
            organ.Body = ent;
            Dirty(args.Entity, organ);
        }

        var refresh = new AppendageWearInventoryRefreshEvent(); // Funky - CyberMed
        RaiseLocalEvent(ent.Owner, ref refresh); // Funky - CyberMed

        RecalculateBlindnessFromOrgans(ent.Owner); // Funky - CyberMed
    }

    private void OnBodyEntRemoved(Entity<BodyComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        if (!_organQuery.TryComp(args.Entity, out var organ))
            return;

        // Funky - CyberMed: Start
        // Clear BodyPart.Body and propagate removal to child organs
        if (_bodyPartQuery.TryComp(args.Entity, out var bodyPart))
        {
            if (bodyPart.Organs != null)
            {
                foreach (var child in bodyPart.Organs.ContainedEntities.ToArray())
                {
                    if (_organQuery.TryComp(child, out var childOrgan) && childOrgan.Body != null)
                    {
                        var bodyEv = new OrganRemovedFromEvent(child);
                        RaiseLocalEvent(ent, ref bodyEv);
                        var notifyEv = new OrganRemovedFromBodyNotifyEvent(child);
                        RaiseLocalEvent(ent, ref notifyEv);
                        var removeEv = new OrganGotRemovedEvent(ent);
                        RaiseLocalEvent(child, ref removeEv);
                        childOrgan.Body = null;
                        Dirty(child, childOrgan);
                    }
                }
            }
            bodyPart.Body = null;
            Dirty(args.Entity, bodyPart);
        }
        // Funky - CyberMed: End

        var body = new OrganRemovedFromEvent(args.Entity);
        RaiseLocalEvent(ent, ref body);
        var bodyNotify = new OrganRemovedFromBodyNotifyEvent(args.Entity);
        RaiseLocalEvent(ent, ref bodyNotify);

        var ev = new OrganGotRemovedEvent(ent);
        RaiseLocalEvent(args.Entity, ref ev);

        if (organ.Body == null)
            return;

        organ.Body = null;
        Dirty(args.Entity, organ);

        var refreshRemoved = new AppendageWearInventoryRefreshEvent(); // Funky - CyberMed
        RaiseLocalEvent(ent.Owner, ref refreshRemoved); // Funky - CyberMed

        RecalculateBlindnessFromOrgans(ent.Owner); // Funky - CyberMed
    }

    // Funky - CyberMed: Start
    private void OnBodyPartEntInserted(Entity<BodyPartComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        var rootBody = ent.Comp.Body;
        if (!rootBody.HasValue || !Exists(rootBody))
            return;

        if (!_organQuery.TryComp(args.Entity, out var organ))
            return;

        var body = new OrganInsertedIntoEvent(args.Entity);
        RaiseLocalEvent(rootBody.Value, ref body);

        var ev = new OrganGotInsertedEvent(rootBody.Value);
        RaiseLocalEvent(args.Entity, ref ev);

        if (organ.Body != rootBody)
        {
            organ.Body = rootBody;
            Dirty(args.Entity, organ);
        }

        var refreshPartIn = new AppendageWearInventoryRefreshEvent();
        RaiseLocalEvent(rootBody.Value, ref refreshPartIn);

        RecalculateBlindnessFromOrgans(rootBody.Value);
    }

    private void OnBodyPartEntRemoved(Entity<BodyPartComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        var rootBody = ent.Comp.Body;
        if (!rootBody.HasValue || !Exists(rootBody))
            return;

        if (!_organQuery.TryComp(args.Entity, out var organ))
            return;

        var body = new OrganRemovedFromEvent(args.Entity);
        RaiseLocalEvent(rootBody.Value, ref body);
        var bodyNotify = new OrganRemovedFromBodyNotifyEvent(args.Entity);
        RaiseLocalEvent(rootBody.Value, ref bodyNotify);

        var ev = new OrganGotRemovedEvent(rootBody.Value);
        RaiseLocalEvent(args.Entity, ref ev);

        if (organ.Body == null)
            return;

        organ.Body = null;
        Dirty(args.Entity, organ);

        var refreshPartOut = new AppendageWearInventoryRefreshEvent();
        RaiseLocalEvent(rootBody.Value, ref refreshPartOut);

        RecalculateBlindnessFromOrgans(rootBody.Value);
    }
    // Funky - CyberMed: End

    private void OnCanDrag(Entity<BodyComponent> ent, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    // Funky - CyberMed: Start
    private void OnBodyPartQuery(Entity<BodyComponent> ent, ref BodyPartQueryEvent args)
    {
        if (args.Body != ent.Owner || ent.Comp.Organs == null)
            return;

        args.Parts.Clear();
        foreach (var entity in ent.Comp.Organs.ContainedEntities)
        {
            args.Parts.Add(entity);
        }
    }

    private void OnBodyPartQueryByType(Entity<BodyComponent> ent, ref BodyPartQueryByTypeEvent args)
    {
        if (args.Body != ent.Owner || ent.Comp.Organs == null)
            return;

        args.Parts.Clear();
        foreach (var entity in ent.Comp.Organs.ContainedEntities)
        {
            if (!_organQuery.TryComp(entity, out var organ))
                continue;

            if (args.Category is { } category && organ.Category != category)
                continue;

            if (args.Symmetry is { } symmetry && symmetry != BodyPartSymmetry.None)
            {
                var categoryStr = organ.Category?.Id ?? "";
                var isLeft = categoryStr.Contains("Left", System.StringComparison.OrdinalIgnoreCase);
                var isRight = categoryStr.Contains("Right", System.StringComparison.OrdinalIgnoreCase);
                if (symmetry == BodyPartSymmetry.Left && !isLeft)
                    continue;
                if (symmetry == BodyPartSymmetry.Right && !isRight)
                    continue;
            }

            args.Parts.Add(entity);
        }
    }

    /// <summary>
    /// Returns all organs in the body, including those nested inside body parts.
    /// </summary>
    public IEnumerable<EntityUid> GetAllOrgans(EntityUid body)
    {
        if (!_bodyQuery.TryComp(body, out var bodyComp) || bodyComp.Organs == null)
            yield break;

        foreach (var entity in bodyComp.Organs.ContainedEntities)
        {
            yield return entity;
            if (_bodyPartQuery.TryComp(entity, out var bodyPart) && bodyPart.Organs != null)
            {
                foreach (var child in bodyPart.Organs.ContainedEntities)
                    yield return child;
            }
        }
    }

    /// <summary>
    /// Resolves the root body entity from a container that holds organs.
    /// Handles both direct body containers (body_organs) and body part containers (e.g. limb_organs).
    /// </summary>
    public bool TryGetRootBodyFromOrganContainer(BaseContainer container, out EntityUid body)
    {
        body = default;
        if (container.ID == BodyComponent.ContainerID && HasComp<BodyComponent>(container.Owner))
        {
            body = container.Owner;
            return true;
        }
        if (TryComp<BodyPartComponent>(container.Owner, out var bodyPart) &&
            bodyPart.Body is { } bodyUid &&
            Exists(bodyUid) &&
            container.ID == bodyPart.ContainerId)
        {
            body = bodyUid;
            return true;
        }
        return false;
    }
    // Funky - CyberMed: End
}
