using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.EntityEffects.Effects.StatusEffects.Components;

/// <remarks>
/// Only fitting they go in the same file
/// </remarks>

public sealed partial class Insulated : EntityEffectBase<Insulated>
{
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-insulated", ("chance", Probability));
}

public sealed partial class BudgetInsulated : EntityEffectBase<BudgetInsulated>
{
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-budget-insulated", ("chance", Probability));
}
