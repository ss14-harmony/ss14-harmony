using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Xenograft;

/// <summary>
/// Marks an organ's donor species for xenograft matching and foreign-host effectiveness.
/// </summary>
[RegisterComponent]
[Access(typeof(OrganXenograftSystem))]
public sealed partial class OrganXenograftComponent : Component
{
    /// <summary>
    /// Species this organ is naturally tuned for (matches donor creature species id).
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SpeciesPrototype> NativeSpecies;

    /// <summary>
    /// Effectiveness when implanted in a foreign species. Multiplies metabolism scale (&lt;1 penalty, &gt;1 bonus).
    /// </summary>
    [DataField]
    public float ForeignQualityDefault = 0.6f;

    /// <summary>
    /// Per-recipient overrides for <see cref="ForeignQualityDefault"/>.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SpeciesPrototype>, float> ForeignQualityOverrides = new();
}
