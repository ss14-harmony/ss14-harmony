using Content.Shared.Cybernetics.Systems;

namespace Content.Shared.Cybernetics.Components;

/// <summary>
/// Marks an item as a cyber limb module. Used when stored in cyber limb storage.
/// </summary>
[RegisterComponent]
[Access(typeof(CyberLimbModuleSystem))]
public sealed partial class CyberLimbModuleComponent : Component
{
    [DataField]
    public CyberLimbModuleType ModuleType { get; private set; }
}

public enum CyberLimbModuleType
{
    MatterBin,
    Manipulator,
    Capacitor,
    Cpu,
    Battery, // Reserved for future
}
