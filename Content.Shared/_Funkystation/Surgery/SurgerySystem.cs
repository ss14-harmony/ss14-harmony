using System.Linq;
using System.Numerics;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Integrity.Events;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery;

public sealed class SurgerySystem : EntitySystem
{
    private static readonly string[] DetachLimbCategories = ["ArmLeft", "ArmRight", "LegLeft", "LegRight"];

    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SurgeryLayerSystem _surgeryLayer = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, SurgeryRequestEvent>(OnSurgeryRequest);
        SubscribeLocalEvent<BodyComponent, SurgeryDoAfterEvent>(OnSurgeryDoAfter);
        SubscribeLocalEvent<SurgeryLayerComponent, SurgeryStepCompletedEvent>(OnSurgeryStepCompleted);
    }

    private void OnSurgeryRequest(Entity<BodyComponent> ent, ref SurgeryRequestEvent args)
    {
        if (args.Target != ent.Owner)
            return;

        args.Valid = false;

        if (!Exists(args.User) || !Exists(args.Target) || !Exists(args.BodyPart))
        {
            args.RejectReason = "invalid-entity";
            return;
        }

        var query = new BodyPartQueryEvent(ent.Owner);
        RaiseLocalEvent(ent.Owner, ref query);
        var isAttachLimbToEmptySlot = args.ProcedureId == "AttachLimb" && args.BodyPart == ent.Owner;
        if (!isAttachLimbToEmptySlot && !query.Parts.Contains(args.BodyPart))
        {
            args.RejectReason = "body-part-not-in-body";
            return;
        }

        // Slime-specific: cannot receive organ or limb implants (design doc: Slime-Specific Systems)
        if (TryComp<HumanoidProfileComponent>(ent.Owner, out var humanoidProfile))
        {
            if (humanoidProfile.Species == (ProtoId<SpeciesPrototype>)"SlimePerson" &&
                (args.ProcedureId == "InsertOrgan" || args.ProcedureId == "AttachLimb"))
            {
                args.RejectReason = "slime-cannot-receive-implants";
                return;
            }

        }

        SurgeryProcedurePrototype? procedure = null;
        SurgeryStepPrototype? step = null;
        if (_prototypes.TryIndex(args.ProcedureId, out var proc))
        {
            procedure = proc;
            if (procedure.Layer != args.Layer)
            {
                args.RejectReason = "layer-mismatch";
                return;
            }
        }
        else if (_prototypes.TryIndex<SurgeryStepPrototype>(args.ProcedureId.ToString(), out var s))
        {
            step = s;
            if (step.Layer != args.Layer)
            {
                args.RejectReason = "layer-mismatch";
                return;
            }
        }
        else
        {
            args.RejectReason = "unknown-step";
            return;
        }

        var stepLayer = procedure?.Layer ?? step!.Layer;

        EntityUid? tool = null;
        var isImprovised = false;
        if (procedure != null)
        {
            if (!procedure.RequiresTool || procedure.PrimaryTool.IsHand)
            {
                // AttachLimb: limb in hand counts as "tool". InsertOrgan/RemoveOrgan: no tool needed.
                if (args.ProcedureId == "AttachLimb" && args.Organ.HasValue)
                {
                    foreach (var held in _hands.EnumerateHeld((args.User, null)))
                    {
                        if (held == args.Organ.Value)
                        {
                            tool = held;
                            break;
                        }
                    }
                }
                // If we still don't have a tool, that's ok for procedures like InsertOrgan, RemoveOrgan
            }
            else
            {
                // Primary: Tag or DamageType
                if (procedure.PrimaryTool.Tag.HasValue)
                {
                    var primaryTag = procedure.PrimaryTool.Tag.Value;
                    foreach (var held in _hands.EnumerateHeld((args.User, null)))
                    {
                        if (_tag.HasTag(held, primaryTag))
                        {
                            tool = held;
                            break;
                        }
                    }
                }
                else if (procedure.PrimaryTool.DamageType.HasValue)
                {
                    var damageKey = procedure.PrimaryTool.DamageType.Value switch
                    {
                        ImprovisedDamageType.Slash => "Slash",
                        ImprovisedDamageType.Heat => "Heat",
                        ImprovisedDamageType.Blunt => "Blunt",
                        _ => null
                    };
                    if (damageKey != null)
                    {
                        foreach (var held in _hands.EnumerateHeld((args.User, null)))
                        {
                            var damage = _melee.GetDamage(held, args.User);
                            if (damage.DamageDict.TryGetValue(damageKey, out var val) && val.Float() > 0)
                            {
                                tool = held;
                                break;
                            }
                        }
                    }
                }

                if (!tool.HasValue)
                {
                    foreach (var improvised in procedure.ImprovisedTools)
                    {
                        if (improvised.Tag.HasValue)
                        {
                            foreach (var held in _hands.EnumerateHeld((args.User, null)))
                            {
                                if (_tag.HasTag(held, improvised.Tag.Value))
                                {
                                    tool = held;
                                    isImprovised = true;
                                    break;
                                }
                            }
                        }
                        else if (improvised.DamageType.HasValue)
                        {
                            var damageKey = improvised.DamageType.Value switch
                            {
                                ImprovisedDamageType.Slash => "Slash",
                                ImprovisedDamageType.Heat => "Heat",
                                ImprovisedDamageType.Blunt => "Blunt",
                                _ => null
                            };
                            if (damageKey != null)
                            {
                                foreach (var held in _hands.EnumerateHeld((args.User, null)))
                                {
                                    var damage = _melee.GetDamage(held, args.User);
                                    if (damage.DamageDict.TryGetValue(damageKey, out var val) && val.Float() > 0)
                                    {
                                        tool = held;
                                        isImprovised = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (tool.HasValue)
                            break;
                    }
                }
                if (!tool.HasValue)
                {
                    args.RejectReason = "missing-tool";
                    return;
                }
            }
        }
        else if (!string.IsNullOrEmpty(step!.RequiredToolTag))
        {
            var requiredTag = new ProtoId<TagPrototype>(step.RequiredToolTag);
            foreach (var held in _hands.EnumerateHeld((args.User, null)))
            {
                if (_tag.HasTag(held, requiredTag))
                {
                    tool = held;
                    break;
                }
            }
            if (!tool.HasValue && step.ImprovisedToolRequiresBluntDamage)
            {
                foreach (var held in _hands.EnumerateHeld((args.User, null)))
                {
                    var damage = _melee.GetDamage(held, args.User);
                    if (damage.DamageDict.TryGetValue("Blunt", out var blunt) && blunt.Float() > 0)
                    {
                        tool = held;
                        isImprovised = true;
                        break;
                    }
                }
            }
            if (!tool.HasValue && step.ImprovisedToolTags.Count > 0)
            {
                foreach (var improvisedTagStr in step.ImprovisedToolTags)
                {
                    var improvisedTag = new ProtoId<TagPrototype>(improvisedTagStr);
                    foreach (var held in _hands.EnumerateHeld((args.User, null)))
                    {
                        if (_tag.HasTag(held, improvisedTag))
                        {
                            tool = held;
                            isImprovised = true;
                            break;
                        }
                    }
                    if (tool.HasValue)
                        break;
                }
            }
            if (!tool.HasValue)
            {
                args.RejectReason = "missing-tool";
                return;
            }
        }

        BodyPartSurgeryStepsPrototype? stepsConfig;
        SurgeryLayerComponent? layerComp = null;

        if (isAttachLimbToEmptySlot)
        {
            if (!args.Organ.HasValue || !Exists(args.Organ.Value))
            {
                args.RejectReason = "invalid-entity";
                return;
            }
            if (!TryComp<OrganComponent>(args.Organ.Value, out var limbOrgan) || limbOrgan.Category is not { } limbCategory)
            {
                args.RejectReason = "invalid-limb-type";
                return;
            }
            if (!TryComp<HumanoidProfileComponent>(ent.Owner, out var speciesProfile))
            {
                args.RejectReason = "unknown-species-or-category";
                return;
            }
            stepsConfig = _surgeryLayer.GetStepsConfig(speciesProfile.Species, limbCategory);
        }
        else
        {
            layerComp = EnsureComp<SurgeryLayerComponent>(args.BodyPart);
            var performedList = stepLayer switch
            {
                SurgeryLayer.Skin => layerComp.PerformedSkinSteps,
                SurgeryLayer.Tissue => layerComp.PerformedTissueSteps,
                SurgeryLayer.Organ => layerComp.PerformedOrganSteps,
                _ => null
            };
            stepsConfig = _surgeryLayer.GetStepsConfig(ent.Owner, args.BodyPart);
            if (performedList != null && performedList.Contains(args.ProcedureId.ToString()))
            {
                if (stepsConfig == null || !_surgeryLayer.CanPerformStep(args.ProcedureId.ToString(), stepLayer, layerComp, stepsConfig, args.BodyPart))
                {
                    args.RejectReason = "already-done";
                    return;
                }
            }
        }

        if (stepsConfig == null)
        {
            args.RejectReason = "unknown-species-or-category";
            return;
        }

        if (!isAttachLimbToEmptySlot && layerComp != null &&
            !_surgeryLayer.CanPerformStep(args.ProcedureId.ToString(), stepLayer, layerComp, stepsConfig, args.BodyPart, args.Organ))
        {
            args.RejectReason = "layer-not-open";
            return;
        }

        if (args.ProcedureId == "DetachLimb")
        {
            if (!TryComp<OrganComponent>(args.BodyPart, out var limbOrgan) || limbOrgan.Category is not { } limbCategory)
            {
                args.RejectReason = "invalid-body-part";
                return;
            }
            if (!DetachLimbCategories.Contains(limbCategory.ToString()))
            {
                args.RejectReason = "cannot-detach-limb";
                return;
            }
            if (!TryComp<BodyPartComponent>(args.BodyPart, out var limbBodyPart) || limbBodyPart.Body != ent.Owner)
            {
                args.RejectReason = "body-part-detached";
                return;
            }
        }
        else if (args.ProcedureId == "AttachLimb")
        {
            if (!args.Organ.HasValue || !Exists(args.Organ.Value))
            {
                args.RejectReason = "invalid-entity";
                return;
            }
            if (!TryComp<OrganComponent>(args.Organ.Value, out var limbOrgan) || limbOrgan.Body.HasValue)
            {
                args.RejectReason = "organ-already-in-body";
                return;
            }
            if (limbOrgan.Category is not { } limbCategory || !DetachLimbCategories.Contains(limbCategory.ToString()))
            {
                args.RejectReason = "invalid-limb-type";
                return;
            }
            if (!_hands.IsHolding(args.User, args.Organ.Value))
            {
                args.RejectReason = "limb-not-in-hand";
                return;
            }
        }

        var triggersOrganRemoval = procedure?.TriggersOrganRemoval ?? args.ProcedureId == "RemoveOrgan";
        var stepId = args.ProcedureId.ToString();
        var organForCheck = args.Organ;
        var isOrganRemovalProc = procedure != null && organForCheck.HasValue && TryComp<OrganSurgeryProceduresComponent>(organForCheck.Value, out var organRemovalProcs)
            && organRemovalProcs.RemovalProcedures.Any(p => p.ToString() == stepId);

        if (triggersOrganRemoval || isOrganRemovalProc)
        {
            if (!args.Organ.HasValue || !Exists(args.Organ.Value))
            {
                args.RejectReason = "invalid-entity";
                return;
            }
            if (!TryComp<BodyPartComponent>(args.BodyPart, out var bodyPartComp) || bodyPartComp.Organs == null ||
                !bodyPartComp.Organs.ContainedEntities.Contains(args.Organ.Value))
            {
                args.RejectReason = "organ-not-in-body-part";
                return;
            }
            if (bodyPartComp.Body != ent.Owner)
            {
                args.RejectReason = "body-part-detached";
                return;
            }
        }
        else if (procedure != null && stepId != "AttachLimb" && organForCheck.HasValue && TryComp<OrganSurgeryProceduresComponent>(organForCheck.Value, out var organInsertProcs) && organInsertProcs.InsertionProcedures.Any(p => p.ToString() == stepId))
        {
            // Organ insertion mend steps (OrganInsertHemostat, etc.) require the organ to already be in the body.
            // AttachLimb is excluded: the limb is in hand, not in the body.
            if (!Exists(organForCheck.Value))
            {
                args.RejectReason = "invalid-entity";
                return;
            }
            if (!TryComp<BodyPartComponent>(args.BodyPart, out var bodyPartComp) || bodyPartComp.Organs == null ||
                !bodyPartComp.Organs.ContainedEntities.Contains(organForCheck.Value))
            {
                args.RejectReason = "organ-not-in-body-part";
                return;
            }
        }
        else if (args.ProcedureId == "InsertOrgan")
        {
            if (!args.Organ.HasValue || !Exists(args.Organ.Value))
            {
                args.RejectReason = "invalid-entity";
                return;
            }
            if (!TryComp<OrganComponent>(args.Organ.Value, out var organComp) || organComp.Body.HasValue)
            {
                args.RejectReason = "organ-already-in-body";
                return;
            }
            if (!_hands.IsHolding(args.User, args.Organ.Value))
            {
                args.RejectReason = "organ-not-in-hand";
                return;
            }
            if (!TryComp<BodyPartComponent>(args.BodyPart, out var insertBodyPart) || insertBodyPart.Organs == null)
            {
                args.RejectReason = "body-part-no-container";
                return;
            }
            if (insertBodyPart.Body != ent.Owner)
            {
                args.RejectReason = "body-part-detached";
                return;
            }
            if (insertBodyPart.Slots.Count > 0)
            {
                if (organComp.Category is not { } category || !insertBodyPart.Slots.Contains(category))
                {
                    args.RejectReason = "no-slot-for-organ";
                    return;
                }
                if (insertBodyPart.Organs != null && insertBodyPart.Organs.ContainedEntities.Any(o =>
                        TryComp<OrganComponent>(o, out var oComp) && oComp.Category == category))
                {
                    args.RejectReason = "slot-filled";
                    return;
                }
            }
        }

        var stepRequestEv = new SurgeryStepRequestEvent(args.User, ent.Owner, args.ProcedureId, args.Layer, args.Organ, stepsConfig);
        RaiseLocalEvent(args.BodyPart, ref stepRequestEv);
        if (!stepRequestEv.Valid)
        {
            args.RejectReason = stepRequestEv.RejectReason ?? "layer-not-open";
            return;
        }

        args.UsedImprovisedTool = isImprovised;
        args.ToolUsed = tool;

        // InsertOrgan/AttachLimb: organ/limb is in hand - use it as DoAfter "used" for tracking.
        // RemoveOrgan and organ removal procedures (Retractor/Scalpel/Hemostat): organ is in-body. Use body part as DoAfter "used"
        // so InRangeUnobstructed succeeds (organ inside body would block the ray from user to organ).
        var isOrganStep = args.ProcedureId == "InsertOrgan" || args.ProcedureId == "AttachLimb" || triggersOrganRemoval || isOrganRemovalProc;
        var isOrganInBody = args.ProcedureId == "RemoveOrgan" || isOrganRemovalProc;
        var toolForSound = tool; // Save for surgery sound (tool's melee hit sound) before reassignment
        if (isOrganStep && args.Organ.HasValue)
        {
            if (isOrganInBody)
                tool = args.BodyPart; // Organ in body - use body part for range check
            else
                tool = args.Organ.Value;
        }

        var doAfterEv = new SurgeryDoAfterEvent(GetNetEntity(args.BodyPart), args.ProcedureId, args.Organ.HasValue ? GetNetEntity(args.Organ.Value) : null, isImprovised, toolForSound.HasValue ? GetNetEntity(toolForSound.Value) : null);
        var delayMultiplier = 1f;
        float baseDelay;
        if (procedure != null)
        {
            baseDelay = procedure.PrimaryTool.DoAfterDelay;
            if (isImprovised && tool.HasValue)
            {
                foreach (var improvised in procedure.ImprovisedTools)
                {
                    if (improvised.DamageType == ImprovisedDamageType.Blunt && improvised.BluntSpeedBaseline is { } baseline)
                    {
                        var damage = _melee.GetDamage(tool.Value, args.User);
                        if (damage.DamageDict.TryGetValue("Blunt", out var bluntVal) && bluntVal.Float() > 0)
                        {
                            var blunt = Math.Max(1f, bluntVal.Float());
                            delayMultiplier = Math.Clamp(baseline / blunt, 0.5f, 3f);
                        }
                        else
                            delayMultiplier = improvised.DelayMultiplier;
                        break;
                    }
                    else if (improvised.DelayMultiplier > 0)
                    {
                        delayMultiplier = improvised.DelayMultiplier;
                        break;
                    }
                }
            }
        }
        else
        {
            baseDelay = step!.DoAfterDelay;
            if (isImprovised)
            {
                if (step.ImprovisedBluntSpeedBaseline is { } baseline && tool.HasValue)
                {
                    var damage = _melee.GetDamage(tool!.Value, args.User);
                    if (damage.DamageDict.TryGetValue("Blunt", out var bluntVal) && bluntVal.Float() > 0)
                    {
                        var blunt = Math.Max(1f, bluntVal.Float());
                        delayMultiplier = Math.Clamp(baseline / blunt, 0.5f, 3f);
                    }
                    else
                        delayMultiplier = step.ImprovisedDelayMultiplier;
                }
                else
                    delayMultiplier = step.ImprovisedDelayMultiplier;
            }
        }
        var delay = baseDelay * delayMultiplier;
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, delay, doAfterEv, args.Target, args.Target, tool)
        {
            NeedHand = true,
            BreakOnHandChange = !isOrganStep,
            BreakOnMove = !isOrganStep,
            BreakOnDropItem = !isOrganStep,
            DistanceThreshold = isOrganStep ? 3f : 1.5f,
            RequireCanInteract = !isOrganStep,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            args.RejectReason = "doafter-failed";
            return;
        }

        args.Valid = true;
    }

    private void OnSurgeryDoAfter(Entity<BodyComponent> ent, ref SurgeryDoAfterEvent args)
    {
        if (args.Cancelled || args.Target == null)
            return;

        var bodyPart = GetEntity(args.BodyPart);
        if (!Exists(bodyPart))
            return;

        SurgeryProcedurePrototype? procedure = null;
        SurgeryStepPrototype? step = null;
        if (_prototypes.TryIndex(args.ProcedureId, out var proc))
            procedure = proc;
        else if (_prototypes.TryIndex<SurgeryStepPrototype>(args.ProcedureId.ToString(), out var s))
            step = s;
        else
            return;

        var stepLayer = procedure?.Layer ?? step!.Layer;
        var isAttachLimbToEmptySlot = args.ProcedureId == "AttachLimb" && bodyPart == ent.Owner;

        var organUid = args.Organ.HasValue ? GetEntity(args.Organ.Value) : (EntityUid?)null;

        if (!isAttachLimbToEmptySlot)
        {
            var layerComp = EnsureComp<SurgeryLayerComponent>(bodyPart);
            var performedList = stepLayer switch
            {
                SurgeryLayer.Skin => layerComp.PerformedSkinSteps,
                SurgeryLayer.Tissue => layerComp.PerformedTissueSteps,
                SurgeryLayer.Organ => layerComp.PerformedOrganSteps,
                _ => null
            };

            var stepsConfig = _surgeryLayer.GetStepsConfig(ent.Owner, bodyPart);
            if (performedList != null && performedList.Contains(args.ProcedureId.ToString()) && (stepsConfig == null || !_surgeryLayer.CanPerformStep(args.ProcedureId.ToString(), stepLayer, layerComp, stepsConfig, bodyPart, organUid)))
                return;

            if (stepsConfig == null || !_surgeryLayer.CanPerformStep(args.ProcedureId.ToString(), stepLayer, layerComp, stepsConfig, bodyPart, organUid))
            {
                _popup.PopupClient(Loc.GetString("health-analyzer-surgery-error-invalid-surgical-process"), args.User, args.User, PopupType.Medium);
                return;
            }
        }
        var stepIdForCheck = args.ProcedureId.ToString();
        var triggersOrganRemoval = procedure?.TriggersOrganRemoval ?? false;
        var isOrganProcedure = organUid.HasValue && TryComp<OrganSurgeryProceduresComponent>(organUid.Value, out var organProcsForStep)
            && (organProcsForStep.RemovalProcedures.Any(p => p.ToString() == stepIdForCheck) || organProcsForStep.InsertionProcedures.Any(p => p.ToString() == stepIdForCheck));
        var isOrganStep = args.ProcedureId == "RemoveOrgan" || args.ProcedureId == "InsertOrgan" || args.ProcedureId == "DetachLimb" || args.ProcedureId == "AttachLimb" || triggersOrganRemoval || isOrganProcedure;

        var penalty = procedure?.Penalty ?? step?.Penalty ?? 0;
        var damage = procedure?.Damage ?? procedure?.PrimaryTool.Damage ?? step?.Damage;
        var healAmount = procedure?.HealAmount ?? procedure?.PrimaryTool.HealAmount ?? step?.HealAmount;

        if (isOrganStep)
            ApplyOrganStep(ent, bodyPart, args.ProcedureId.ToString(), args.Organ, organUid, procedure, step, args.User);
        else
        {
            var completedEv = new SurgeryStepCompletedEvent(args.User, ent.Owner, bodyPart, args.ProcedureId, stepLayer, organUid, step, procedure);
            RaiseLocalEvent(bodyPart, ref completedEv);
            if (!completedEv.Handled)
                return;
        }

        var penaltyRequestEv = new UnsanitarySurgeryPenaltyRequestEvent(ent.Owner, bodyPart, args.ProcedureId.ToString(), stepLayer, args.IsImprovised, step, procedure);
        RaiseLocalEvent(ent.Owner, ref penaltyRequestEv);

        if (damage is { } d && !d.Empty)
            _damageable.TryChangeDamage(ent.Owner, d, ignoreResistances: false, origin: args.User);

        if (healAmount is { } h && !h.Empty)
            _damageable.TryChangeDamage(ent.Owner, h, ignoreResistances: true, origin: args.User);

        // Play the tool's melee hit sound (scalpel sounds like scalpel, crowbar sounds like crowbar)
        SoundSpecifier? sound = null;
        if (args.Tool.HasValue && TryGetEntity(args.Tool.Value, out var toolEnt) && TryComp<MeleeWeaponComponent>(toolEnt, out var melee))
            sound = melee.HitSound ?? melee.NoDamageSound;
        if (sound != null)
            _audio.PlayPredicted(sound, ent.Owner, args.User);
        else
            _audio.PlayPredicted(new SoundCollectionSpecifier("WeakHit"), ent.Owner, args.User);
    }

    private void ApplyOrganStep(Entity<BodyComponent> ent, EntityUid bodyPart, string stepId, NetEntity? organNet, EntityUid? organUid, SurgeryProcedurePrototype? procedure, SurgeryStepPrototype? step, EntityUid user)
    {
        var penalty = procedure?.Penalty ?? step?.Penalty ?? 0;
        var isAttachLimbToEmptySlot = stepId == "AttachLimb" && bodyPart == ent.Owner;
        var triggersOrganRemoval = procedure?.TriggersOrganRemoval ?? false;
        BodyPartComponent? bodyPartComp = null;
        SurgeryLayerComponent? layerComp = null;

        if (!isAttachLimbToEmptySlot)
        {
            if (!TryComp<BodyPartComponent>(bodyPart, out bodyPartComp) || bodyPartComp.Body != ent.Owner)
            {
                _popup.PopupClient(Loc.GetString("health-analyzer-surgery-error-invalid-surgical-process"), user, user, PopupType.Medium);
                return;
            }

            layerComp = EnsureComp<SurgeryLayerComponent>(bodyPart);
            if (layerComp.PerformedOrganSteps.Contains(stepId))
                return;
            if (organNet.HasValue && organUid.HasValue && TryComp<OrganSurgeryProceduresComponent>(organUid.Value, out var organProcs))
            {
                if (organProcs.RemovalProcedures.Any(p => p.ToString() == stepId))
                {
                    var entry = layerComp.OrganRemovalProgress.FirstOrDefault(e => e.Organ == organNet.Value);
                    if (entry != null && entry.Steps.Contains(stepId))
                        return;
                }
                else if (organProcs.InsertionProcedures.Any(p => p.ToString() == stepId))
                {
                    var entry = layerComp.OrganInsertProgress.FirstOrDefault(e => e.Organ == organNet.Value);
                    if (entry != null && entry.Steps.Contains(stepId))
                        return;
                }
            }
        }

        if (triggersOrganRemoval || stepId == "RemoveOrgan")
        {
            if (organUid is not { } organ || !Exists(organ))
            {
                _popup.PopupClient(Loc.GetString("health-analyzer-surgery-error-organ-gone"), user, user, PopupType.Medium);
                return;
            }
            if (bodyPartComp!.Organs == null || !bodyPartComp.Organs.ContainedEntities.Contains(organ))
            {
                _popup.PopupClient(Loc.GetString("health-analyzer-surgery-error-organ-gone"), user, user, PopupType.Medium);
                return;
            }
            var removeEv = new OrganRemoveRequestEvent(organ) { Destination = Transform(user).Coordinates };
            RaiseLocalEvent(organ, ref removeEv);
            if (removeEv.Success)
            {
                if (!_hands.TryPickupAnyHand(user, organ, checkActionBlocker: false))
                    Transform(organ).Coordinates = Transform(user).Coordinates;
                if (triggersOrganRemoval)
                {
                    // Store removal steps on the organ so re-insertion only shows repair steps
                    var removalSteps = layerComp!.OrganRemovalProgress
                        .FirstOrDefault(e => e.Organ == organNet!.Value)?.Steps.ToList() ?? new List<string>();
                    if (!removalSteps.Contains(stepId))
                        removalSteps.Add(stepId);
                    var stateComp = EnsureComp<OrganRemovedSurgeryStateComponent>(organ);
                    stateComp.PerformedRemovalSteps = removalSteps;
                    Dirty(organ, stateComp);
                    ClearOrganRemovalProgress(layerComp, organNet!.Value);
                }
                else
                    AddOrganRemovalProgress(layerComp!, organNet!.Value, stepId);
                // Clear organ progress when organ leaves the body so re-insertion shows correct procedures
                ClearOrganInsertProgress(layerComp!, organNet!.Value);
                if (layerComp != null)
                    Dirty(bodyPart, layerComp);
                var penaltyEv = new SurgeryPenaltyAppliedEvent(bodyPart, penalty);
                RaiseLocalEvent(bodyPart, ref penaltyEv);
                var uiRefreshEv = new SurgeryUiRefreshRequestEvent();
                RaiseLocalEvent(ent.Owner, ref uiRefreshEv);
            }
        }
        else if (stepId == "InsertOrgan")
        {
            if (organUid is not { } organ || !Exists(organ))
            {
                _popup.PopupClient(Loc.GetString("health-analyzer-surgery-error-organ-not-in-hand"), user, user, PopupType.Medium);
                return;
            }
            if (!_hands.IsHolding(user, organ))
            {
                _popup.PopupClient(Loc.GetString("health-analyzer-surgery-error-organ-not-in-hand"), user, user, PopupType.Medium);
                return;
            }
            if (TryComp<OrganComponent>(organ, out var organComp) && organComp.Category is { } category &&
                bodyPartComp!.Slots.Count > 0 && bodyPartComp.Organs != null &&
                bodyPartComp.Organs.ContainedEntities.Any(o =>
                    TryComp<OrganComponent>(o, out var oComp) && oComp.Category == category))
            {
                _popup.PopupClient(Loc.GetString("health-analyzer-surgery-error-slot-filled"), user, user, PopupType.Medium);
                return;
            }
            var insertEv = new OrganInsertRequestEvent(bodyPart, organ);
            RaiseLocalEvent(bodyPart, ref insertEv);
            if (insertEv.Success)
            {
                layerComp!.PerformedOrganSteps.Add(stepId);
                if (organNet.HasValue)
                    EnsureOrganInsertEntry(layerComp, organNet.Value);
                Dirty(bodyPart, layerComp);
                var penaltyEv = new SurgeryPenaltyAppliedEvent(bodyPart, penalty);
                RaiseLocalEvent(bodyPart, ref penaltyEv);
                var uiRefreshEv = new SurgeryUiRefreshRequestEvent();
                RaiseLocalEvent(ent.Owner, ref uiRefreshEv);
            }
            else
                _popup.PopupClient(Loc.GetString("health-analyzer-surgery-error-slot-filled"), user, user, PopupType.Medium);
        }
        else if (stepId == "DetachLimb")
        {
            EntityCoordinates dest;
            Angle? localRotation = null;

            // Use map coordinates and AlignToGrid so limbs always drop on the floor/grid,
            // never on the bed or other object the patient may be buckled to.
            // Fallback: patient not on a grid (e.g. in space) — use surgeon's position.
            var mapCoords = !TryComp(ent.Owner, out TransformComponent? patientXform) || patientXform.GridUid == null
                ? _transform.GetMapCoordinates(user)
                : _transform.GetMapCoordinates(ent.Owner);

            if (_standing.IsDown(ent.Owner) && TryComp<OrganComponent>(bodyPart, out var limbOrganComp) && limbOrganComp.Category is { } limbCategory)
            {
                var categoryStr = limbCategory.ToString();
                var bodyRot = _transform.GetWorldRotation(ent.Owner);
                // Use Y-axis (perpendicular to body) so limbs drop to the side when laying down
                var offset = categoryStr is "ArmLeft" or "LegLeft"
                    ? bodyRot.RotateVec(new Vector2(0, -0.35f))
                    : bodyRot.RotateVec(new Vector2(0, 0.35f));
                mapCoords = new MapCoordinates(mapCoords.Position + offset, mapCoords.MapId);
                localRotation = bodyRot + Angle.FromDegrees(-90);
            }
            else
            {
                localRotation = null;
            }

            dest = _map.AlignToGrid(_transform.ToCoordinates(mapCoords));

            // Detach limb organs (hand/foot) first so they drop as separate items
            if (TryComp<BodyPartComponent>(bodyPart, out var limbBodyPart) && limbBodyPart.Organs != null)
            {
                foreach (var limbOrgan in limbBodyPart.Organs.ContainedEntities.ToArray())
                {
                    var limbRemoveEv = new OrganRemoveRequestEvent(limbOrgan) { Destination = dest, LocalRotation = localRotation };
                    RaiseLocalEvent(limbOrgan, ref limbRemoveEv);
                }
            }
            var removeEv = new OrganRemoveRequestEvent(bodyPart) { Destination = dest, LocalRotation = localRotation };
            RaiseLocalEvent(bodyPart, ref removeEv);
            if (removeEv.Success)
            {
                layerComp!.PerformedOrganSteps.Add(stepId);
                Dirty(bodyPart, layerComp);
                var penaltyEv = new SurgeryPenaltyAppliedEvent(bodyPart, penalty);
                RaiseLocalEvent(bodyPart, ref penaltyEv);
                var uiRefreshEv = new SurgeryUiRefreshRequestEvent();
                RaiseLocalEvent(ent.Owner, ref uiRefreshEv);
            }
        }
        else if (stepId == "AttachLimb")
        {
            if (organUid is not { } limb || !Exists(limb) || !_hands.IsHolding(user, limb))
            {
                _popup.PopupClient(Loc.GetString("health-analyzer-surgery-error-organ-not-in-hand"), user, user, PopupType.Medium);
                return;
            }
            if (TryComp<OrganComponent>(limb, out var limbOrganComp) && limbOrganComp.Category is { } attachCategory)
            {
                var alreadyHasLimb = _body.GetAllOrgans(ent.Owner).Any(o =>
                    TryComp<OrganComponent>(o, out var oComp) && oComp.Category == attachCategory);
                if (alreadyHasLimb)
                {
                    _popup.PopupClient(Loc.GetString("health-analyzer-surgery-error-slot-filled"), user, user, PopupType.Medium);
                    return;
                }
            }
            if (ent.Comp.Organs != null && _container.Insert(limb, ent.Comp.Organs))
            {
                _hands.TryDrop(user, limb);
                if (!isAttachLimbToEmptySlot)
                {
                    layerComp!.PerformedOrganSteps.Add(stepId);
                    Dirty(bodyPart, layerComp);
                }
                var limbName = Identity.Name(limb, EntityManager, user);
                _popup.PopupClient(Loc.GetString("health-analyzer-surgery-organ-fully-attached", ("organName", limbName)), user, user, PopupType.Medium);
                var penaltyEv = new SurgeryPenaltyAppliedEvent(limb, penalty);
                RaiseLocalEvent(limb, ref penaltyEv);
                var uiRefreshEv = new SurgeryUiRefreshRequestEvent();
                RaiseLocalEvent(ent.Owner, ref uiRefreshEv);
            }
            else
                _popup.PopupClient(Loc.GetString("health-analyzer-surgery-error-slot-filled"), user, user, PopupType.Medium);
        }
        else if (procedure != null && layerComp != null && organUid.HasValue && organNet.HasValue)
        {
            if (TryComp<OrganSurgeryProceduresComponent>(organUid.Value, out var organProcs))
            {
                if (organProcs.RemovalProcedures.Any(p => p.ToString() == stepId))
                {
                    AddOrganRemovalProgress(layerComp, organNet.Value, stepId);
                }
                else if (organProcs.InsertionProcedures.Any(p => p.ToString() == stepId))
                {
                    AddOrganInsertProgress(layerComp, organNet.Value, stepId);
                    // Organ is fully repaired; clear removal state so future removals show removal steps
                    var insertSteps = layerComp.OrganInsertProgress.FirstOrDefault(e => e.Organ == organNet.Value)?.Steps ?? [];
                    if (organProcs.InsertionProcedures.All(p => insertSteps.Contains(p.ToString())))
                    {
                        RemComp<OrganRemovedSurgeryStateComponent>(organUid.Value);
                        var organName = Identity.Name(organUid.Value, EntityManager, user);
                        _popup.PopupClient(Loc.GetString("health-analyzer-surgery-organ-fully-attached", ("organName", organName)), user, user, PopupType.Medium);
                    }
                }
            }
            Dirty(bodyPart, layerComp);
            if (penalty > 0)
            {
                var penaltyEv = new SurgeryPenaltyAppliedEvent(bodyPart, penalty);
                RaiseLocalEvent(bodyPart, ref penaltyEv);
            }
            var uiRefreshEv = new SurgeryUiRefreshRequestEvent();
            RaiseLocalEvent(ent.Owner, ref uiRefreshEv);
        }
    }

    private static void AddOrganRemovalProgress(SurgeryLayerComponent comp, NetEntity organ, string stepId)
    {
        var entry = comp.OrganRemovalProgress.FirstOrDefault(e => e.Organ == organ);
        if (entry == null)
        {
            comp.OrganRemovalProgress.Add(new OrganProgressEntry { Organ = organ, Steps = new List<string> { stepId } });
        }
        else
        {
            if (!entry.Steps.Contains(stepId))
                entry.Steps.Add(stepId);
        }
    }

    private static void ClearOrganRemovalProgress(SurgeryLayerComponent comp, NetEntity organ)
    {
        comp.OrganRemovalProgress.RemoveAll(e => e.Organ == organ);
    }

    private static void ClearOrganInsertProgress(SurgeryLayerComponent comp, NetEntity organ)
    {
        comp.OrganInsertProgress.RemoveAll(e => e.Organ == organ);
    }

    private static void EnsureOrganInsertEntry(SurgeryLayerComponent comp, NetEntity organ)
    {
        if (comp.OrganInsertProgress.Any(e => e.Organ == organ))
            return;
        comp.OrganInsertProgress.Add(new OrganProgressEntry { Organ = organ, Steps = new List<string>() });
    }

    private static void AddOrganInsertProgress(SurgeryLayerComponent comp, NetEntity organ, string stepId)
    {
        var entry = comp.OrganInsertProgress.FirstOrDefault(e => e.Organ == organ);
        if (entry == null)
        {
            comp.OrganInsertProgress.Add(new OrganProgressEntry { Organ = organ, Steps = new List<string> { stepId } });
        }
        else
        {
            if (!entry.Steps.Contains(stepId))
                entry.Steps.Add(stepId);
        }
    }

    private void OnSurgeryStepCompleted(Entity<SurgeryLayerComponent> ent, ref SurgeryStepCompletedEvent args)
    {
        if (args.Handled)
            return;

        var layerComp = ent.Comp;
        var performedList = args.Layer switch
        {
            SurgeryLayer.Skin => layerComp.PerformedSkinSteps,
            SurgeryLayer.Tissue => layerComp.PerformedTissueSteps,
            SurgeryLayer.Organ => layerComp.PerformedOrganSteps,
            _ => null
        };

        if (performedList == null || performedList.Contains(args.ProcedureId.ToString()))
            return;

        var stepsConfig = _surgeryLayer.GetStepsConfig(args.Target, ent.Owner);
        var closeStepIds = args.Layer switch
        {
            SurgeryLayer.Skin => stepsConfig?.GetSkinCloseStepIds(_prototypes),
            SurgeryLayer.Tissue => stepsConfig?.GetTissueCloseStepIds(_prototypes),
            _ => null
        };

        var closeStepId = args.ProcedureId.ToString();
        if (closeStepIds != null && closeStepIds.Contains(closeStepId))
        {
            string? undoesStepId = null;
            int openStepPenalty = 0;
            var isLegacyRemoveAll = false;

            if (_prototypes.TryIndex<SurgeryStepPrototype>(closeStepId, out var closeStep))
            {
                undoesStepId = closeStep.UndoesStep;
                isLegacyRemoveAll = undoesStepId == null;
                if (undoesStepId != null && _prototypes.TryIndex<SurgeryStepPrototype>(undoesStepId, out var openStep))
                    openStepPenalty = openStep.Penalty;
            }
            else if (_prototypes.TryIndex<SurgeryProcedurePrototype>(closeStepId, out var closeProc) && closeProc.UndoesProcedure is { } undoesProc)
            {
                undoesStepId = undoesProc.ToString();
                if (_prototypes.TryIndex<SurgeryProcedurePrototype>(undoesStepId, out var openProc))
                    openStepPenalty = openProc.Penalty;
                else if (_prototypes.TryIndex<SurgeryStepPrototype>(undoesStepId, out var openStep))
                    openStepPenalty = openStep.Penalty;
            }

            if (undoesStepId != null && !isLegacyRemoveAll)
            {
                var penaltyToRemove = 0;
                if (performedList.Contains(undoesStepId))
                {
                    performedList.Remove(undoesStepId);
                    penaltyToRemove += openStepPenalty;
                }

                // Do not cascade-remove later steps when repairing an earlier step. Later steps stay in their
                // performed state; the user must re-do the undone step before interacting with later layers again.

                if (penaltyToRemove > 0)
                {
                    var removeEv = new SurgeryPenaltyRemovedEvent(ent.Owner, penaltyToRemove);
                    RaiseLocalEvent(ent.Owner, ref removeEv);
                }
            }
            else if (isLegacyRemoveAll)
            {
                // Legacy: remove all open steps
                var openStepIds = args.Layer switch
                {
                    SurgeryLayer.Skin => stepsConfig!.GetSkinOpenStepIds(_prototypes),
                    SurgeryLayer.Tissue => stepsConfig!.GetTissueOpenStepIds(_prototypes),
                    _ => Array.Empty<string>()
                };

                var penaltyToRemove = 0;
                foreach (var openId in openStepIds)
                {
                    if (!performedList.Contains(openId))
                        continue;
                    if (_prototypes.TryIndex<SurgeryProcedurePrototype>(openId, out var openProc))
                        penaltyToRemove += openProc.Penalty;
                    else if (_prototypes.TryIndex<SurgeryStepPrototype>(openId, out var openStep))
                        penaltyToRemove += openStep.Penalty;
                    performedList.Remove(openId);
                }

                if (penaltyToRemove > 0)
                {
                    var removeEv = new SurgeryPenaltyRemovedEvent(ent.Owner, penaltyToRemove);
                    RaiseLocalEvent(ent.Owner, ref removeEv);
                }
            }
        }

        // When re-performing an open step, remove the close step that undoes it so it can be performed again
        var stepIdToAdd = args.ProcedureId.ToString();
        if (closeStepIds != null)
        {
            foreach (var undoCloseStepId in closeStepIds)
            {
                if (!performedList.Contains(undoCloseStepId))
                    continue;
                var undoesOpen = false;
                if (_prototypes.TryIndex<SurgeryProcedurePrototype>(undoCloseStepId, out var closeProc))
                    undoesOpen = closeProc.UndoesProcedure?.ToString() == stepIdToAdd;
                else if (_prototypes.TryIndex<SurgeryStepPrototype>(undoCloseStepId, out var closeStep))
                    undoesOpen = closeStep.UndoesStep == stepIdToAdd;
                if (undoesOpen)
                {
                    performedList.Remove(undoCloseStepId);
                    break; // at most one close step undoes a given open step
                }
            }
        }

        performedList.Add(stepIdToAdd);
        Dirty(ent, layerComp);

        var penalty = args.Procedure?.Penalty ?? args.Step?.Penalty ?? 0;
        var penaltyEv = new SurgeryPenaltyAppliedEvent(ent.Owner, penalty);
        RaiseLocalEvent(ent.Owner, ref penaltyEv);

        args.Handled = true;

        var uiRefreshEv = new SurgeryUiRefreshRequestEvent();
        RaiseLocalEvent(args.Target, ref uiRefreshEv);
    }
}
