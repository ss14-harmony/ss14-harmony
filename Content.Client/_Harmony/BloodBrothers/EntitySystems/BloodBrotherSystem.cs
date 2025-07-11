using Content.Shared._Harmony.BloodBounds.Components;
using Content.Shared._Harmony.BloodBounds.EntitySystems;
using Content.Shared.Antag;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Harmony.BloodBounds.EntitySystems;

public sealed class BloodBoundSystem : SharedBloodBoundSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBoundComponent, GetStatusIconsEvent>(OnBloodBoundGetIcons);
    }

    private void OnBloodBoundGetIcons(Entity<BloodBoundComponent> entity, ref GetStatusIconsEvent args)
    {
        if (_playerManager.LocalSession?.AttachedEntity is { } playerEntity)
        {
            if (!HasComp<ShowAntagIconsComponent>(playerEntity) &&
                entity.Owner != playerEntity &&
                entity.Comp.Bound != playerEntity)
                return;
        }

        if (_prototypeManager.TryIndex(entity.Comp.BloodBoundIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
