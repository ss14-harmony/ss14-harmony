using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.LimbRegeneration.Components;

[RegisterComponent]
public sealed partial class SlimeLimbRegenerationComponent : Component
{
    [DataField]
    public TimeSpan RegenerationDelay { get; set; } = TimeSpan.FromMinutes(2);

    [DataField]
    public List<SlimeLimbRegenerationEntry> PendingRegenerations { get; set; } = new();
}

public record struct SlimeLimbRegenerationEntry(ProtoId<OrganCategoryPrototype> Category, TimeSpan RegenerationStartTime);
