using Robust.Shared.Prototypes;

namespace Content.Shared._Box.Metabolism;

/// <summary>
/// Metabolism Category identifier used to determine if a specific entity and a specific reagent are compatible.
/// </summary>
[Prototype]
public sealed partial class MetabolismCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name", required: true)]
    private LocId Name { get; set; }

    [ViewVariables(VVAccess.ReadOnly)]
    public string LocalizedName => Loc.GetString(Name);
}
