using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Damage;
using Content.Shared.Humanoid;

namespace Content.Shared.Cybernetics.Systems;

/// <summary>
/// Applies cyber limb damage overlay state (BloodDisabled) when cyber limbs are attached.
/// Limb sprite visuals come from VisualOrganComponent on the organ; VisualBodySystem applies them.
/// </summary>
public sealed class CyberLimbAppearanceSystem : EntitySystem
{
    private static readonly IReadOnlyDictionary<string, HumanoidVisualLayers[]> CategoryToLayers = new Dictionary<string, HumanoidVisualLayers[]>
    {
        ["ArmLeft"] = [HumanoidVisualLayers.LArm, HumanoidVisualLayers.LHand],
        ["ArmRight"] = [HumanoidVisualLayers.RArm, HumanoidVisualLayers.RHand],
        ["LegLeft"] = [HumanoidVisualLayers.LLeg],
        ["LegRight"] = [HumanoidVisualLayers.RLeg],
    };

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CyberLimbComponent, OrganGotInsertedEvent>(OnCyberLimbInserted);
        SubscribeLocalEvent<CyberLimbComponent, OrganGotRemovedEvent>(OnCyberLimbRemoved);
    }

    private void OnCyberLimbInserted(Entity<CyberLimbComponent> ent, ref OrganGotInsertedEvent args)
    {
        var body = args.Target;
        if (!TryComp<OrganComponent>(ent, out var organ) || organ.Category is not { } category)
            return;

        var categoryStr = category.ToString();
        if (!CategoryToLayers.TryGetValue(categoryStr, out var layers))
            return;

        if (LifeStage(body) >= EntityLifeStage.Terminating)
            return;

        if (TryComp<AppearanceComponent>(body, out var appearance))
        {
            foreach (var layer in layers)
            {
                _appearance.SetData(body, layer, DamageOverlayLayerState.BloodDisabled, appearance);
            }
            _appearance.SetData(body, DamageVisualizerKeys.ForceUpdate, true, appearance);
        }
    }

    private void OnCyberLimbRemoved(Entity<CyberLimbComponent> ent, ref OrganGotRemovedEvent args)
    {
        // Do not set damage overlay appearance data on remove - LimbDetachmentEffectsSystem
        // sets AllDisabled when the organ is removed; we must not overwrite that.
        // Limb visibility is handled by VisualBodySystem (sets layer to Invalid).
    }
}
