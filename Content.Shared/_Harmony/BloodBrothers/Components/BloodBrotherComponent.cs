using Content.Shared._Harmony.BloodBrothers.EntitySystems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.BloodBrothers.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedBloodBrotherSystem))]
public sealed partial class BloodBrotherComponent : Component
{
    [DataField]
    public ProtoId<FactionIconPrototype> BloodBrotherIcon = "BloodBrotherFaction";

    [DataField]
    public ProtoId<NpcFactionPrototype> BloodBrotherFaction = "BloodBrother";

    [DataField]
    public EntProtoId<MindRoleComponent> BloodBrotherMindRole = "MindRoleBloodBrother";

    [DataField]
    public LocId BriefingText = "blood-brother-briefing";

    [DataField]
    public Color BriefingColor = Color.Red; // TODO: find the right color.

    // TODO: get a sound

    public override bool SessionSpecific => true;
}
