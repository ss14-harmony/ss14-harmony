using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Prototypes;

/// <summary>
/// Defines a surgical tool for UI display. Maps tag to locale key.
/// Lookup by Tag or by ID (when id matches tag).
/// </summary>
[Prototype]
public sealed partial class SurgicalToolPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Tag that items must have to be considered this tool.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<TagPrototype> Tag { get; private set; }

    /// <summary>
    /// Locale key for UI display (e.g. health-analyzer-surgery-tool-scalpel).
    /// </summary>
    [DataField(required: true)]
    public LocId DisplayName { get; private set; }
}
