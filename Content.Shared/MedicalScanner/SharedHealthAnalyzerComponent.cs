using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.MedicalScanner;

/// <summary>
/// Shared component for health analyzer UI. Holds ScannedEntity for surgery request validation.
/// Kept in sync by server's HealthAnalyzerSystem.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[ComponentProtoName("SharedHealthAnalyzer")]
public sealed partial class SharedHealthAnalyzerComponent : Component
{
    /// <summary>
    /// Which entity has been scanned, for surgery request validation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ScannedEntity { get; set; }
}
