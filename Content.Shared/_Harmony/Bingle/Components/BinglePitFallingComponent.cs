using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Harmony.Bingle.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BinglePitFallingComponent : Component
{
    /// <summary>
    /// Whether the entity has finished falling into the pit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Fell;

    /// <summary>
    ///     Original scale of the object so it can be restored if the component is removed in the middle of the animation
    /// </summary>
    [DataField]
    public Vector2 OriginalScale = Vector2.Zero;

    /// <summary>
    ///     Scale that the animation should bring entities to.
    /// </summary>
    [DataField]
    public Vector2 AnimationScale = new(0.01f, 0.01f);

    /// <summary>
    ///     Time it should take for the falling animation (scaling down) to complete.
    /// </summary>
    [DataField]
    public TimeSpan AnimationTime = TimeSpan.FromSeconds(1.5f);

    /// <summary>
    /// The time at which the entity with this component will fall into the pit.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan InsertIntoPitTime = TimeSpan.Zero;

    /// <summary>
    /// The pit this entity is falling into.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Pit = EntityUid.Invalid;
}
