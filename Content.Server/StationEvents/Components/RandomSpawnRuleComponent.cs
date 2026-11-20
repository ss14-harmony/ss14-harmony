using Content.Server.StationEvents.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Content.Shared.Radio; // Harmony Change - Moffstation Dead Drop Port

namespace Content.Server.StationEvents.Components;

/// <summary>
/// Spawns a single entity at a random tile on a station using TryGetRandomTile.
/// </summary>
[RegisterComponent, Access(typeof(RandomSpawnRule))]
public sealed partial class RandomSpawnRuleComponent : Component
{
    /// <summary>
    /// The entity to be spawned.
    /// </summary>
    [DataField("prototype", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Prototype = string.Empty;

    // Harmony Change Start - Moffstation Dead Drop Port
    /// <summary>
    /// The radio message to send when spawning the entity. The entity is used as the sender of the radio message.
    /// </summary>
    [DataField]
    public RandomSpawnRuleRadioMessage? RadioMessage;
    // Harmony Change End
}

// Harmony Change Start - Moffstation Dead Drop Port
/// <param name="Channel">The channel to send the message over</param>
/// <param name="Message">The message to send. Is localized with a <c>location</c> argument.</param>
[DataRecord]
public sealed partial record RandomSpawnRuleRadioMessage(
    [field: DataField(required: true)]
    ProtoId<RadioChannelPrototype> Channel,
    [field: DataField(required: true)]
    LocId Message
);
// Harmony Change End
