using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.EntityEffects.Effects.StatusEffects.Components;

public sealed partial class Insulated : EntityEffectBase<Insulated>
{
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-insulated", ("chance", Probability));
}
