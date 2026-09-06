namespace Content.Shared._Impstation.Pleebnar.Components;
/// <summary>
/// exists only to mark an entity as gibbable by pleebnars
/// </summary>
[RegisterComponent]
public sealed partial class PleebnarGibbableComponent : Component
{
    [DataField]//used to overwrite parent gibbable components to protect specific things like hamlet
    public bool Mindshield = false;
}
