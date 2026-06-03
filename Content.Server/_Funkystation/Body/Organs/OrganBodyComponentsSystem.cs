using Content.Shared._Funkystation.Body.Organs;
using Content.Shared.Body;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager;

namespace Content.Server._Funkystation.Body.Organs;

/// <summary>
/// Applies YAML-listed components from <see cref="OrganBodyComponentsComponent"/> onto the host body
/// when the organ is inserted and removes them when it leaves (server authoritative).
/// </summary>
public sealed partial class OrganBodyComponentsSystem : EntitySystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private ISerializationManager _serializationManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganBodyComponentsComponent, OrganGotInsertedEvent>(OnOrganInserted);
        SubscribeLocalEvent<OrganBodyComponentsComponent, OrganGotRemovedEvent>(OnOrganRemoved);
    }

    private void OnOrganInserted(Entity<OrganBodyComponentsComponent> ent, ref OrganGotInsertedEvent args)
    {
        var body = args.Target;
        if (!Exists(body) || Terminating(body))
            return;

        foreach (var (name, data) in ent.Comp.Components)
        {
            var newComp = (Component) _componentFactory.GetComponent(name);

            // If the body already has it (another organ, gear, etc.), leave the existing instance.
            if (HasComp(body, newComp.GetType()))
                continue;

            var temp = (object) newComp;
            _serializationManager.CopyTo(data.Component, ref temp);
            AddComp(body, (Component) temp!);
        }
    }

    private void OnOrganRemoved(Entity<OrganBodyComponentsComponent> ent, ref OrganGotRemovedEvent args)
    {
        var body = args.Target;
        if (!Exists(body) || Terminating(body))
            return;

        foreach (var (name, _) in ent.Comp.Components)
        {
            var compType = _componentFactory.GetComponent(name).GetType();
            if (!HasComp(body, compType))
                continue;

            RemComp(body, compType);
        }
    }
}
