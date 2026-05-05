using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Medical.Integrity.Components;

/// <summary>
/// Added to organs when Immunosuppressant is metabolized. Increases the body's effective integrity capacity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(BioRejectionSystem), typeof(Content.Shared.EntityEffects.Effects.Body.AddIntegrityImmunityBoostEntityEffectSystem))]
public sealed partial class IntegrityImmunityBoostComponent : Component
{
    /// <summary>
    /// Amount added to effective integrity capacity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Amount { get; set; }

    /// <summary>
    /// When the boost expires. BioRejectionSystem ignores expired boosts.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt { get; set; }
}
