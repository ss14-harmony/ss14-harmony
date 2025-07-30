using Content.Server._Umbra.Atmos.EntitySystems;

namespace Content.Server._Umbra.Atmos.Components;

[RegisterComponent]
[Access(typeof(SpontaneousCombustionSystem))]
public sealed partial class SpontaneousCombustionProtectionComponent : Component
{
    /// <summary>
    /// Reduces firestacks gained by a percentage.
    /// </summary>
    [DataField]
    public int ProtectionPercent = 75;

    public record struct GetSpontaneousCombustionProtectionValuesEvent
    {
        public int ProtectionPercent;
    }
}
