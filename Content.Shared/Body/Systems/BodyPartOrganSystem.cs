using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Robust.Shared.Containers;
using Robust.Shared.Log;

namespace Content.Shared.Body;

public sealed class BodyPartOrganSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private EntityQuery<OrganComponent> _organQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyPartComponent, OrganInsertRequestEvent>(OnOrganInsertRequest);
        SubscribeLocalEvent<OrganComponent, OrganRemoveRequestEvent>(OnOrganRemoveRequest);

        _organQuery = GetEntityQuery<OrganComponent>();
    }

    private void OnOrganInsertRequest(Entity<BodyPartComponent> ent, ref OrganInsertRequestEvent args)
    {
        if (args.BodyPart != ent.Owner)
            return;

        args.Success = false;

        if (!_organQuery.TryComp(args.Organ, out var organComp))
            return;

        if (organComp.Body.HasValue)
            return;

        if (ent.Comp.Organs == null)
            return;

        if (organComp.Category is not { } category)
            return;

        if (ent.Comp.Slots.Count > 0)
        {
            if (!ent.Comp.Slots.Contains(category))
                return;

            foreach (var existing in ent.Comp.Organs.ContainedEntities)
            {
                if (_organQuery.TryComp(existing, out var existingOrgan) && existingOrgan.Category == category)
                {
                    return;
                }
            }
        }

        args.Success = _container.Insert(args.Organ, ent.Comp.Organs);
    }

    private void OnOrganRemoveRequest(Entity<OrganComponent> ent, ref OrganRemoveRequestEvent args)
    {
        if (args.Organ != ent.Owner)
            return;

        args.Success = false;

        BaseContainer? container = null;

        // Organs in body_organs (limbs, grafted limbs): use Body to get container directly
        if (ent.Comp.Body is { } body && _container.TryGetContainer(body, BodyComponent.ContainerID, out var bodyContainer, null) && bodyContainer.Contains(ent.Owner))
        {
            container = bodyContainer;
        }
        // Organs in body part (e.g. hand in arm): use parent's containment
        else if (_container.TryGetContainingContainer(Transform(ent).ParentUid, ent.Owner, out var parentContainer))
        {
            container = parentContainer;
        }
        // Fallback for grafted limbs: Organ.Body may be unset or container lookup may fail; find container directly
        else if (_container.TryGetContainingContainer((ent.Owner, null, null), out var directContainer))
        {
            container = directContainer;
        }

        if (container == null)
            return;

        args.Success = _container.Remove((ent.Owner, (TransformComponent?)null, (MetaDataComponent?)null), container, destination: args.Destination, localRotation: args.LocalRotation);
    }
}
