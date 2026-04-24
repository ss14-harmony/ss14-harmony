using Robust.Shared.Serialization;
using Content.Shared.Actions;

namespace Content.Shared.Cybernetics.Events;

[ByRefEvent]
public record struct EmptyHandActivateEvent(EntityUid User, string? HandName, bool AltInteract = false)
{
    public bool Handled { get; set; }
}

public sealed partial class OpenCyberArmStorageActionEvent : InstantActionEvent
{
}
