using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared.Cybernetics.Systems;

/// <summary>
/// Adds "Open [limb name]" verbs when right-clicking a body with cyber limbs, when the maintenance panel is open and bolts are tight.
/// </summary>
public sealed class CyberLimbStorageVerbSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberneticsMaintenanceComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CyberLimbComponent, BoundUserInterfaceCheckRangeEvent>(OnCyberLimbStorageRangeCheck, after: [typeof(SharedInteractionSystem)]);
    }

    /// <summary>
    /// When another player opens cyber limb storage, the default range check fails because the limb is in the body's
    /// organ container (not a storage container), so IsAccessible returns false. Override to allow access when the
    /// actor is in range of the body that contains the limb.
    /// </summary>
    private void OnCyberLimbStorageRangeCheck(Entity<CyberLimbComponent> ent, ref BoundUserInterfaceCheckRangeEvent args)
    {
        if (args.Result != BoundUserInterfaceRangeResult.Fail)
            return;

        if (args.UiKey is not StorageComponent.StorageUiKey.Key)
            return;

        if (!_container.TryGetContainingContainer(ent.Owner, out var container) ||
            !HasComp<BodyComponent>(container.Owner) || container.ID != BodyComponent.ContainerID)
            return;

        var body = container.Owner;
        var range = args.Data.InteractionRange;
        if (range <= 0)
            return;

        if (_interaction.InRangeUnobstructed(args.Actor!.Owner, body, range))
            args.Result = BoundUserInterfaceRangeResult.Pass;
    }

    private void OnGetVerbs(Entity<CyberneticsMaintenanceComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var comp = ent.Comp;
        if (!comp.PanelOpen || !comp.BoltsTight)
            return;

        var body = ent.Owner;

        var user = args.User;
        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (!HasComp<CyberLimbComponent>(organ) || !TryComp<StorageComponent>(organ, out var storage))
                continue;

            var limbUid = organ;
            var storageComp = storage;
            var limbName = Name(organ);
            var verb = new ActivationVerb
            {
                Act = () => _storage.OpenStorageUI(limbUid, user, storageComp, false),
                Text = Loc.GetString("cyber-maintenance-verb-open-limb", ("limbName", limbName)),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/open.svg.192dpi.png"))
            };
            args.Verbs.Add(verb);
        }
    }
}
