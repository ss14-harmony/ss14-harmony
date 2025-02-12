using System.Numerics;
using Content.Server.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server._Harmony.Maps.Additions;

[Prototype]
public sealed class MapAdditionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<GameMapPrototype> ApplyOn { get; private set; }

    [DataField]
    public List<MapAdditionEntity> Entities { get; private set; } = new();
}

[DataDefinition]
public sealed partial class MapAdditionEntity
{
    [DataField(required: true)]
    public ProtoId<EntityPrototype> Prototype { get; private set; }

    [DataField(required: true)]
    public Vector2 Position { get; private set; }

    [DataField]
    public Angle Rotation { get; private set; }

    [DataField]
    public ComponentRegistry Components { get; private set; }
}
