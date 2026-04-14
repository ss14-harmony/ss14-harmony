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

        var parent = Transform(ent).ParentUid;
        if (!_container.TryGetContainingContainer(parent, ent.Owner, out var container))
            return;

        args.Success = _container.Remove((ent.Owner, (TransformComponent?)null, (MetaDataComponent?)null), container, destination: args.Destination, localRotation: args.LocalRotation);
    }
}
