using System.Linq;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Tracks organ-specific procedure progress for removal/insertion flows.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class OrganProgressEntry
{
    [DataField]
    public NetEntity Organ { get; set; }

    [DataField]
    public List<string> Steps { get; set; } = new();
}

/// <summary>
/// Tracks surgery layer state on a body part. Stores which steps have been performed per layer.
/// Used to determine which steps are available and which layers are "open".
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SurgerySystem), typeof(SurgeryLayerSystem), typeof(Content.Shared.Body.BodySystem))]
public sealed partial class SurgeryLayerComponent : Component
{
    /// <summary>
    /// Step IDs that have been performed for the Skin layer (e.g. RetractSkin).
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> PerformedSkinSteps { get; set; } = new();

    /// <summary>
    /// Step IDs that have been performed for the Tissue layer (e.g. RetractTissue, SawBones).
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> PerformedTissueSteps { get; set; } = new();

    /// <summary>
    /// Step IDs that have been performed for the Organ layer (e.g. RemoveOrgan, DetachLimb).
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> PerformedOrganSteps { get; set; } = new();

    /// <summary>
    /// Per-organ removal procedure progress (e.g. OrganRemovalRetractor, OrganRemovalScalpel).
    /// Cleared when organ is removed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<OrganProgressEntry> OrganRemovalProgress { get; set; } = new();

    /// <summary>
    /// Per-organ insertion procedure progress (e.g. OrganInsertHemostat, OrganInsertSearing).
    /// Used for organs recently inserted that need mend steps.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<OrganProgressEntry> OrganInsertProgress { get; set; } = new();

    /// <summary>
    /// True if RetractSkin has been performed and not yet closed (skin layer open for tissue).
    /// </summary>
    public bool SkinRetracted => PerformedSkinSteps.Contains("RetractSkin") && !PerformedSkinSteps.Contains("ReleaseRetractor");

    /// <summary>
    /// True if RetractTissue has been performed and not yet closed (tissue layer open for organ).
    /// </summary>
    public bool TissueRetracted => PerformedTissueSteps.Contains("RetractTissue") && !PerformedTissueSteps.Any(s => s is "MaintainAlignment" or "SealBleedPoints" or "RepairBoneSection");

    /// <summary>
    /// True if last tissue open step (RetractTissue) has been performed and tissue not closed (organ layer fully open).
    /// </summary>
    public bool BonesSawed => PerformedTissueSteps.Contains("RetractTissue") && !PerformedTissueSteps.Any(s => s is "MaintainAlignment" or "SealBleedPoints" or "RepairBoneSection");
}
