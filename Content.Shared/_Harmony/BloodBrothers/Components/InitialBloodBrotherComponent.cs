using Content.Shared._Harmony.BloodBrothers.EntitySystems;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.BloodBrothers.Components;

/// <summary>
/// Signifies that an entity is the blood brother chosen by a game-rule.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedBloodBrotherSystem))]
[AutoGenerateComponentState]
public sealed partial class InitialBloodBrotherComponent : Component
{
    [DataField]
    public EntProtoId<EntityTargetActionComponent> ConvertAction = "ActionBloodBrotherConvert";

    [DataField, AutoNetworkedField]
    public EntityUid? ConvertActionEntity;

    [DataField]
    public LocId MessageConvertFailedNoMind = "blood-brother-convert-failed-no-mind";

    [DataField]
    public LocId MessageConvertFailedNotHumanoid = "blood-brother-convert-failed-no-mind";

    [DataField]
    public LocId MessageConvertFailedZombie = "blood-brother-convert-failed-zombie";

    [DataField]
    public LocId MessageConvertFailedMindShielded = "blood-brother-convert-failed-shielded";

    [DataField]
    public LocId MessageConvertFailedDead = "blood-brother-convert-failed-dead";

    public override bool SendOnlyToOwner => true;
}

public sealed partial class BloodBrotherConvertActionEvent : EntityTargetActionEvent;
