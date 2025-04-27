using Content.Shared._Harmony.BloodBrothers.Components;
using Content.Shared._Harmony.BloodBrothers.EntitySystems;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Harmony.BloodBrothers.EntitySystems;

public sealed class BloodBrotherSystem : SharedBloodBrotherSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBrotherComponent, GetStatusIconsEvent>(OnBloodBrotherGetIcons);
    }

    private void OnBloodBrotherGetIcons(Entity<BloodBrotherComponent> entity, ref GetStatusIconsEvent args)
    {
        if (_prototypeManager.TryIndex(entity.Comp.BloodBrotherIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
