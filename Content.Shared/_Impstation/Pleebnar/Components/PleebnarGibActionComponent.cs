using Robust.Shared.GameStates;

namespace Content.Shared._Impstation.Pleebnar.Components;
/// <summary>
/// gibbing action component, denotes that an entity has access to the pleebnar gibbing action,
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PleebnarGibActionComponent : Component
{
    [DataField]
    public EntityUid? gibAction;

    [DataField]
    public string? gibActionId = "ActionPleebnarGib";

    [DataField]//admin setable bool that makes a pleebnar able to gib anything with a body
    public bool superPleebnar=false;
}
