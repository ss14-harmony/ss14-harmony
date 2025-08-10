using Content.Shared.Chat.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.ItemCurse;

/// <summary>
/// Component for the ItemCurse action. Based off of ItemRecallComponent
/// Used for first marking a held item, and then shocking the holder and surrounding entities when activated.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedItemCurseSystem))]
public sealed partial class ItemCurseComponent : Component
{
    /// <summary>
    /// The name the action should have while an entity is marked.
    /// </summary>
    [DataField]
    public LocId? WhileMarkedName = "item-curse-marked-name";

    /// <summary>
    /// The description the action should have while an entity is marked.
    /// </summary>
    [DataField]
    public LocId? WhileMarkedDescription = "item-curse-marked-description";

    /// <summary>
    /// The name the action starts with.
    /// This shouldn't be set in yaml.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? InitialName;

    /// <summary>
    /// The description the action starts with.
    /// This shouldn't be set in yaml.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? InitialDescription;

    /// <summary>
    /// The entity currently marked to be cursed by this action.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? MarkedEntity;

    /// <summary>
    /// How hard the item will be flung when the curse is activated
    /// </summary>
    [DataField]
    public int FlingStrength = 25;

    /// <summary>
    /// Range of lightning bolts created when the curse is activated
    /// </summary>
    [DataField]
    public int LightningRange = 5;

    /// <summary>
    /// Amount of lightning bolts created when the curse is activated
    /// </summary>
    [DataField]
    public int LightningCount = 3;

    /// <summary>
    /// Prototype used for lightning bolts created when the curse is activated
    /// </summary>
    [DataField]
    public EntProtoId LightningPrototype = "LightningRevenant";

    /// <summary>
    /// Insuls-bypassing shock damage dealt to the holder of the cursed item when the curse is activated
    /// </summary>
    [DataField]
    public int ShockDamage = 15;

    /// <summary>
    /// Insuls-bypassing shock duration for the holder of the cursed item when the curse is activated
    /// </summary>
    [DataField]
    public TimeSpan ShockDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The emote action used when casting the spell
    /// </summary>
    [DataField]
    public ProtoId<EmotePrototype> SpellUseEmote = "Snap";
}
