using Content.Shared.Body;
using Content.Shared.Cloning.Events;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.IdentityManagement;

namespace Content.Shared.Traits.Assorted;

/// <summary>
/// Funky - CyberMed: Entire function re-written to use organ trait blindness and <see cref="BodySystem.RecalculateBlindnessFromOrgans"/>.
/// Handles permanent blindness examine text, flash mitigation, cloning propagation, and cleanup when removed.
/// Mechanical vision (<see cref="BlindableComponent"/>) uses organ trait blindness and <see cref="BodySystem.RecalculateBlindnessFromOrgans"/>.
/// </summary>
public sealed partial class PermanentBlindnessSystem : EntitySystem
{
    [Dependency] private BlindableSystem _blinding = default!;
    [Dependency] private BodySystem _body = default!; // Funky - CyberMed

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PermanentBlindnessComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PermanentBlindnessComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PermanentBlindnessComponent, CloningEvent>(OnCloning);
    }

    private void OnExamined(Entity<PermanentBlindnessComponent> blindness, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange && blindness.Comp.Blindness == 0)
        {
            args.PushMarkup(Loc.GetString("permanent-blindness-trait-examined", ("target", Identity.Entity(blindness, EntityManager))));
        }
    }

    private void OnShutdown(Entity<PermanentBlindnessComponent> blindness, ref ComponentShutdown args)
    {
        if (!TryComp<BlindableComponent>(blindness.Owner, out var blindable))
            return;

        if (blindable.MinDamage != 0)
        {
            _blinding.SetMinDamage((blindness.Owner, blindable), 0);
        }

        // Heal all eye damage when the component is removed.
        // Otherwise you would still be blind, but not *permanently* blind, meaning you have to heal the eye damage with oculine.
        // This is needed for changelings that transform from a blind player to a non-blind one.
        _blinding.AdjustEyeDamage((blindness.Owner, blindable), -blindable.EyeDamage);

        _body.RemoveOrganTraitBlindnessFromImplantedEyes(blindness.Owner);
    }

    private void OnCloning(Entity<PermanentBlindnessComponent> blindness, ref CloningEvent args)
    {
        _body.ApplyOrganTraitBlindnessToImplantedEyes(args.CloneUid, blindness.Comp.Blindness);
        _body.RecalculateBlindnessFromOrgans(args.CloneUid);
    }
}
