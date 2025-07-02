namespace Content.Shared.Tag;

public sealed class AddTagSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AddTagOnMapInitComponent, MapInitEvent>(OnAddTagInit);
    }

    private void OnAddTagInit(Entity<AddTagOnMapInitComponent> ent, ref MapInitEvent args)
    {
        _tag.AddTags(ent.Owner, ent.Comp.Tags);
        RemCompDeferred<AddTagOnMapInitComponent>(ent.Owner);
    }
}
