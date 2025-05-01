using Content.Shared._Harmony.BloodBrothers.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
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

    [DataField]
    public EntProtoId<ObjectiveComponent> ConvertedBrotherObjective = "BloodBrotherConvertedObjective";

    [DataField, AutoNetworkedField]
    public EntityUid? ConvertActionEntity;

    #region Conversion Failure Messages

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

    [DataField]
    public LocId MessageConvertFailedPreference = "blood-brother-convert-failed-preference";

    [DataField]
    public LocId MessageConvertFailedTarget = "blood-brother-convert-failed-target";

    public LocId MessageConvertFailedAlreadyBrother = "blood-brother-convert-failed-already-brother";
    // Am I going too far in this whole "don't hard-code" thing...

    #endregion

    [DataField]
    public bool IgnorePreference;

    [DataField]
    public ProtoId<AntagPrototype> RequiredAntagPreference = "BloodBrother";

    public override bool SendOnlyToOwner => true;
}

public sealed partial class BloodBrotherConvertActionEvent : EntityTargetActionEvent;
