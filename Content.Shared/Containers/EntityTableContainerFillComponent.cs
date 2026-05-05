using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Humanoid.Markings; // Funky - CyberMed

namespace Content.Shared.Containers;

/// <summary>
/// Version of <see cref="ContainerFillComponent"/> that utilizes <see cref="EntityTableSelector"/>
/// </summary>
[RegisterComponent, Access(typeof(ContainerFillSystem), typeof(MarkingManager))] // Funky - CyberMed
public sealed partial class EntityTableContainerFillComponent : Component
{
    [DataField]
    public Dictionary<string, EntityTableSelector> Containers = new();
}
