using Content.Shared._Harmony.BloodBounds.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.BloodBounds.Components;

/// <summary>
/// Signifies that an entity is the blood bound chosen by a game-rule.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedBloodBoundSystem))]
[AutoGenerateComponentState]
public sealed partial class InitialBloodBoundComponent : Component
{
    #region Actions

    [DataField]
    public EntProtoId<EntityTargetActionComponent> ConvertAction = "ActionBloodBoundConvert";

    [DataField, AutoNetworkedField]
    public EntityUid? ConvertActionEntity;

    [DataField]
    public EntProtoId<EntityTargetActionComponent> CheckConvertAction = "ActionBloodBoundCheckConvert";

    [DataField, AutoNetworkedField]
    public EntityUid? CheckConvertActionEntity;

    #endregion

    /// <summary>
    /// The antag preference required for someone to be converted into a blood bound.
    /// If null, the check will be skipped.
    /// </summary>
    [DataField]
    public ProtoId<AntagPrototype>? RequiredAntagPreference = "BloodBoundConvertible";

    /// <summary>
    /// The popup that will happen when a blood bound is converted.
    /// </summary>
    [DataField]
    public LocId ConvertPopupText = "blood-bound-conversion-popup";

    /// <summary>
    /// The time for which the converted will be stunned after being converted.
    /// If null, the converted will not be stunned
    /// </summary>
    [DataField]
    public TimeSpan? ConvertStunTime = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The objective that will be given to the converted.
    /// The target will be automatically set to be the initial blood bound.
    /// </summary>
    [DataField]
    public EntProtoId<ObjectiveComponent> ConvertedBoundObjective = "BloodBoundConvertedObjective";

    #region Briefing

    [DataField]
    public LocId BriefingText = "blood-bound-role-greeting";

    [DataField]
    public Color BriefingColor = Color.MediumVioletRed;

    [DataField]
    public SoundSpecifier BriefingSound = new SoundPathSpecifier("/Audio/_Harmony/Misc/blood_bound_greeting.ogg");

    #endregion

    /// <summary>
    /// The faction that the new blood bound will be added to.
    /// </summary>
    [DataField]
    public ProtoId<NpcFactionPrototype> BloodBoundFaction = "BloodBound";

    /// <summary>
    /// The mind role that will be given to the new blood bound.
    /// </summary>
    [DataField]
    public EntProtoId<MindRoleComponent> BloodBoundMindRole = "MindRoleBloodBound";

    public override bool SendOnlyToOwner => true;
}

public sealed partial class BloodBoundConvertActionEvent : EntityTargetActionEvent;

public sealed partial class BloodBoundCheckConvertActionEvent : EntityTargetActionEvent;
