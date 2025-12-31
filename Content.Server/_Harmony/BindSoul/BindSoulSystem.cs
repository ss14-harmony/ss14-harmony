using System.Runtime.InteropServices.ComTypes;
using Content.Server.Beam;
using Content.Server.Mind;
using Content.Server.Polymorph.Systems;
using Content.Shared._Harmony.BindSoul;
using Content.Shared.Actions;
using Content.Shared.Beam;
using Content.Shared.Fluids.Components;
using Content.Shared.Mobs;

namespace Content.Server._Harmony.BindSoul;

public sealed class BindSoulSystem : SharedBindSoulSystem
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly BeamSystem _beam = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SoulBinderComponent, MobStateChangedEvent>(OnBinderDeath);
    }

    protected override void OnSoulBindActionUse(OnBindSoulActionEvent args)
    {
        base.OnSoulBindActionUse(args);

        if (args.Handled)
        {
            var biner = AddComp<SoulBinderComponent>((EntityUid)_polymorph.PolymorphEntity(args.Performer, args.Polymorph)!);
            biner.BindedItem = args.BindedItem;
            biner.SoulbindAction = args.BindSoulAction;
        }

    }

    private void OnBinderDeath(EntityUid uid, SoulBinderComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState!= MobState.Dead)
            return;

        if (component.BindedItem == null)
            return;

        _mind.TryGetMind(uid, out var mindId, out var mind);

        component.DeathCount++;
        var deathcount = component.DeathCount;

        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(10 * deathcount),
            () =>
            {
                var xform = Transform(component.BindedItem);
                var entity = SpawnAtPosition(component.BinderPrototype, xform.Coordinates);

                _mind.TransferTo(mindId, entity, ghostCheckOverride: true, mind: mind);
                var binder = AddComp<SoulBinderComponent>(entity);

                binder.BindedItem = component.BindedItem;
                binder.DeathCount = deathcount;

                _actions.RemoveAction(component.SoulbindAction);

                _beam.TryCreateBeam(uid, component.BindedItem, component.LinkBeamProto);
            }
        );
    }
}
