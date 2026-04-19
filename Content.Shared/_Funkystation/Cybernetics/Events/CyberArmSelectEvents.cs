using Robust.Shared.Serialization;

namespace Content.Shared.Cybernetics.Events;

[ByRefEvent]
public record struct EmptyHandActivateEvent(EntityUid User, string? HandName, bool AltInteract = false)
{
    public bool Handled { get; set; }
}
