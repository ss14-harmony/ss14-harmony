using System.Numerics;
using Content.Server.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server._Harmony.Maps.Additions;

[Prototype]
public sealed class MapAdditionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// A <see cref="GameMapPrototype"/> ID to automatically apply this addition to.
    /// </summary>
    [DataField]
    public ProtoId<GameMapPrototype>? ApplyOn;

    [DataField(required: true)]
    public List<MapAdditionEntity> Entities = new();
}

[DataDefinition]
public sealed partial class MapAdditionEntity
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public string? Name;

    [DataField]
    public string? Description;

    [DataField(required: true)]
    public Vector2 Position;

    [DataField]
    public Angle? Rotation;

    [DataField]
    public ComponentRegistry? Components;
}
