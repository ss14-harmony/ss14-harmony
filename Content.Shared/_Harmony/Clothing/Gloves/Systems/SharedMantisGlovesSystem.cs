using Content.Shared.Item.ItemToggle.Components;
using Content.Shared._Harmony.Clothing.Gloves.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;

namespace Content.Shared._Harmony.Clothing.Gloves.Systems;

public abstract class SharedMantisGlovesSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MantisGlovesComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnToggled(EntityUid uid, MantisGlovesComponent component, ref ItemToggledEvent args)
    {
        UpdateMantisGlovesState(uid, args.Activated, component);

        if (!TryComp<ClothingComponent>(uid, out var clothing))
            return;

        _clothing.SetEquippedPrefix(uid, args.Activated ? "activated" : null, clothing);
    }

    private void UpdateMantisGlovesState(EntityUid uid, bool activated, MantisGlovesComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var name = activated ? component.ActivatedName : component.DeactivatedName;
        var description = activated ? component.ActivatedDescription : component.DeactivatedDescription;

        if (name != null)
            _metaData.SetEntityName(uid, Loc.GetString(name));

        if (description != null)
            _metaData.SetEntityDescription(uid, Loc.GetString(description));
    }
}
