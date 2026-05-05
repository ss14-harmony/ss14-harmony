using Content.Server.Stack;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.Cybernetics.Systems;

public sealed class CyberLimbStorageSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbComponent, StorageInteractAttemptEvent>(OnStorageInteractAttempt);
        SubscribeLocalEvent<CyberneticsMaintenanceComponent, CyberMaintenanceStateChangedEvent>(OnMaintenanceStateChanged);
        SubscribeLocalEvent<StorageComponent, EntGotInsertedIntoContainerMessage>(OnStorageInserted);
        SubscribeLocalEvent<CyberLimbComponent, ContainerIsInsertingAttemptEvent>(OnContainerInsertAttempt, before: [typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<ItemComponent, EntGotRemovedFromContainerMessage>(OnEntityRemovedFromContainer);
    }

    private void OnStorageInteractAttempt(Entity<CyberLimbComponent> ent, ref StorageInteractAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_container.TryGetContainingContainer(ent.Owner, out var container))
            return;

        if (!HasComp<BodyComponent>(container.Owner) || container.ID != BodyComponent.ContainerID)
            return;

        var body = container.Owner;
        if (TryComp<CyberneticsMaintenanceComponent>(body, out var maint) && maint.PanelOpen && maint.BoltsTight)
            return;

        args.Cancelled = true;
    }

    private void OnMaintenanceStateChanged(Entity<CyberneticsMaintenanceComponent> ent, ref CyberMaintenanceStateChangedEvent args)
    {
        if (!args.PanelClosed && !args.BoltsLoosened)
            return;

        var body = ent.Owner;
        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (!HasComp<CyberLimbComponent>(organ))
                continue;

            _ui.CloseUi(organ, StorageComponent.StorageUiKey.Key);
        }
    }

    private void OnStorageInserted(Entity<StorageComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (!HasComp<CyberLimbComponent>(ent.Owner))
            return;

        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        if (!HasComp<BodyComponent>(args.Container.Owner))
            return;

        _ui.CloseUi(ent.Owner, StorageComponent.StorageUiKey.Key);
    }

    /// <summary>
    /// Failsafe: when an entity is removed from a container inside cyber limb storage (e.g. cycling a bullet from a gun)
    /// and ends up in nullspace (insert into storage failed, AttachToGridOrMap failed), drop it at the body's position.
    /// This prevents items from being deleted when object interactions try to insert into full storage.
    /// </summary>
    private void OnEntityRemovedFromContainer(Entity<ItemComponent> entity, ref EntGotRemovedFromContainerMessage args)
    {
        if (TerminatingOrDeleted(entity))
            return;

        // Check if the container we were removed from belongs to something inside cyber limb storage
        var containerOwner = args.Container.Owner;
        var uid = entity.Owner;
        if (!_container.TryGetContainingContainer(containerOwner, out var outer) ||
            outer.ID != StorageComponent.ContainerId ||
            !HasComp<CyberLimbComponent>(outer.Owner))
        {
            return;
        }

        // Entity ended up in nullspace (insert failed, AttachToGridOrMap detached to nullspace)
        var xform = Transform(uid);
        if (xform.ParentUid.IsValid())
            return;

        // Find the body to drop at
        var cyberLimb = outer.Owner;
        if (!_container.TryGetContainingContainer(cyberLimb, out var bodyContainer) ||
            bodyContainer.ID != BodyComponent.ContainerID ||
            !HasComp<BodyComponent>(bodyContainer.Owner))
        {
            return;
        }

        var body = bodyContainer.Owner;
        var dropCoords = Transform(body).Coordinates;
        _transform.SetCoordinates(uid, xform, dropCoords);
    }

    /// <summary>
    /// When inserting a stackable item (e.g. Supercharged CPU) into cyber limb storage,
    /// automatically de-stack so only 1 goes in per slot. Each module takes one tile.
    /// </summary>
    private void OnContainerInsertAttempt(Entity<CyberLimbComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled || args.Container.ID != StorageComponent.ContainerId)
            return;

        if (!TryComp<StackComponent>(args.EntityUid, out var stack) || stack.Count <= 1)
            return;

        args.Cancel();

        var spawnPos = Transform(ent.Owner).Coordinates;
        var split = _stack.Split((args.EntityUid, stack), 1, spawnPos);
        if (split == null)
            return;

        _container.Insert(split.Value, args.Container);
    }
}
