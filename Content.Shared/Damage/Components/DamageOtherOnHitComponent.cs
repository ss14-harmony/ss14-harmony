using Content.Shared.Damage.Systems;

namespace Content.Shared.Damage.Components;

/// <summary>
/// Makes this entity deal damage when thrown at something.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedDamageOtherOnHitSystem))]
public sealed partial class DamageOtherOnHitComponent : Component
{
    /// <summary>
    /// Whether to ignore damage modifiers.
    /// </summary>
    [DataField]
    public bool IgnoreResistances = false;

    /// <summary>
    /// The damage amount to deal on hit.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier Damage = default!;

    // Harmony Change Start:  Allow Pacifists to throw certain items even if they deal damage

    /// <summary>
    /// Whether pacifists can throw the item.
    /// </summary>
    [DataField]
    public bool PacifiedCanThrow = false;

    // Harmony Change End
}
