using Content.Shared.Body;
using Content.Shared.Cloning.Events;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Medical.Surgery;

namespace Content.Shared.Traits.Assorted;

/// <summary>
/// Mob-level paraplegia trait marker for examine and cloning; mechanical state uses foot organs and
/// <see cref="LimbDetachmentEffectsSystem"/>.
/// </summary>
public sealed class PermanentParaplegiaSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private LimbDetachmentEffectsSystem _limbs = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PermanentParaplegiaComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PermanentParaplegiaComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PermanentParaplegiaComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PermanentParaplegiaComponent, CloningEvent>(OnCloning);
    }

    private void OnStartup(Entity<PermanentParaplegiaComponent> ent, ref ComponentStartup args)
    {
        _body.ApplyTraitParaplegiaToImplantedFeet(ent.Owner);
        _limbs.RefreshFootStateForBody(ent.Owner);
    }

    private void OnShutdown(Entity<PermanentParaplegiaComponent> ent, ref ComponentShutdown args)
    {
        _body.RemoveTraitParaplegiaFromImplantedFeet(ent.Owner);
        _limbs.RefreshFootStateForBody(ent.Owner);
    }

    private void OnExamined(Entity<PermanentParaplegiaComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
        {
            args.PushMarkup(Loc.GetString("permanent-paraplegia-trait-examined",
                ("target", Identity.Entity(ent, EntityManager))));
        }
    }

    private void OnCloning(Entity<PermanentParaplegiaComponent> ent, ref CloningEvent args)
    {
        _body.ApplyTraitParaplegiaToImplantedFeet(args.CloneUid);
        _limbs.RefreshFootStateForBody(args.CloneUid);
    }
}
