using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared._Harmony.Clothing.Gloves.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

namespace Content.Shared._Harmony.Clothing.Gloves.Systems;

public abstract class SharedMantisGlovesSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MantisGlovesComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnToggled(EntityUid uid, MantisGlovesComponent component, ref ItemToggledEvent args)
    {
        UpdateMantisGlovesState(uid, args.Activated, component);
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

[Serializable, NetSerializable]
public sealed class MantisGlovesComponentState : ComponentState
{
    public string? ActivatedName;
    public string? ActivatedDescription;
    public string? DeactivatedName;
    public string? DeactivatedDescription;

    public MantisGlovesComponentState(string? activatedName, string? activatedDescription, string? deactivatedName, string? deactivatedDescription)
    {
        ActivatedName = activatedName;
        ActivatedDescription = activatedDescription;
        DeactivatedName = deactivatedName;
        DeactivatedDescription = deactivatedDescription;
    }
}
