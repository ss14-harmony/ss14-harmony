using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.ItemCurse;

/// <summary>
/// Component for the ItemCurse action.
/// Used for marking a held item and making it do something funny with second action use.
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
    [DataField]
    public string? InitialName;

    /// <summary>
    /// The description the action starts with.
    /// This shouldn't be set in yaml.
    /// </summary>
    [DataField]
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
    public int FlingStrength = 20;

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
    public string LightningPrototype = "LightningRevenant";

    /// <summary>
    /// Shock damage dealt to the holder of the cursed item when the curse is activated
    /// </summary>
    [DataField]
    public int ShockDamage = 15;

    /// <summary>
    /// Shock damage dealt to the holder of the cursed item when the curse is activated
    /// </summary>
    [DataField]
    public TimeSpan ShockDuration = TimeSpan.FromSeconds(3);
}
