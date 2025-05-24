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

    [DataField]
    public bool IgnorePreference;

    [DataField]
    public ProtoId<AntagPrototype> RequiredAntagPreference = "BloodBrother";

    [DataField]
    public LocId ConvertPopupText = "blood-brother-conversion-popup";

    [DataField]
    public TimeSpan ConvertStunTime = TimeSpan.FromSeconds(3);

    public override bool SendOnlyToOwner => true;
}

public sealed partial class BloodBrotherConvertActionEvent : EntityTargetActionEvent;
