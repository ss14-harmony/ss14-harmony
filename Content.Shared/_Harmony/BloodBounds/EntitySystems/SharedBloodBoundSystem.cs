using Content.Shared._Harmony.BloodBounds.Components;
using Content.Shared.Actions;
using Content.Shared.Antag;
using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Shared._Harmony.BloodBounds.EntitySystems;

public abstract class SharedBloodBoundSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InitialBloodBoundComponent, MapInitEvent>(OnInitialBloodBoundMapInit);
        SubscribeLocalEvent<InitialBloodBoundComponent, ComponentShutdown>(OnInitialBloodBoundShutdown);
        SubscribeLocalEvent<BloodBoundComponent, ComponentGetStateAttemptEvent>(OnBloodBoundAttemptGetState);
    }

    private void OnInitialBloodBoundMapInit(Entity<InitialBloodBoundComponent> entity, ref MapInitEvent args)
    {
        _actionsSystem.AddAction(entity, ref entity.Comp.ConvertActionEntity, entity.Comp.ConvertAction);
        _actionsSystem.AddAction(entity, ref entity.Comp.CheckConvertActionEntity, entity.Comp.CheckConvertAction);
        Dirty(entity);
    }

    private void OnInitialBloodBoundShutdown(Entity<InitialBloodBoundComponent> entity, ref ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(entity.Comp.ConvertActionEntity);
        _actionsSystem.RemoveAction(entity.Comp.CheckConvertActionEntity);
    }

    private void OnBloodBoundAttemptGetState(
        Entity<BloodBoundComponent> entity,
        ref ComponentGetStateAttemptEvent args)
    {
        args.Cancelled = !CanGetState(args.Player);
    }

    private bool CanGetState(ICommonSession? player)
    {
        //Apparently this can be null in replays so I am just returning true.
        if (player?.AttachedEntity is not {} uid)
            return true;

        return HasComp<BloodBoundComponent>(uid) || HasComp<ShowAntagIconsComponent>(uid);
    }
}
