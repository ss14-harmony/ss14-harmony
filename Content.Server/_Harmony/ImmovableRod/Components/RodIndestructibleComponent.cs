using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Harmony.ImmovableRod.Components;

/// <summary>
/// Makes an entity deflect immovable rods instead of being destroyed by them.
/// </summary>
[RegisterComponent]
public sealed partial class RodIndestructibleComponent : Component
{
    /// <summary>
    /// Multiplier applied to the reflected rod speed.
    /// </summary>
    [DataField("bounceSpeedMultiplier")]
    public float BounceSpeedMultiplier = 1f;

    /// <summary>
    /// Prevents repeated bounce handling while the rod is still separating.
    /// </summary>
    [DataField("bounceCooldown", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan BounceCooldown = TimeSpan.FromSeconds(0.12f);

    public EntityUid? LastRod;
    public TimeSpan BounceBlockedUntil;
}
