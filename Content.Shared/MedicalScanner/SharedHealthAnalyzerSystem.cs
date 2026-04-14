using System.Linq;
using Content.Shared.IdentityManagement;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.MedicalScanner;

internal static partial class HealthAnalyzerToolDisplay
{
    /// <summary>
    /// Gets the proper and improvised tool display locale keys for a surgery procedure.
    /// Used for missing-tool error messages. Prefer SurgeryProcedurePrototype over SurgeryStepPrototype.
    /// </summary>
    public static (string? Proper, string? Improvised) GetProcedureToolDisplay(
        IPrototypeManager prototypes,
        ProtoId<SurgeryProcedurePrototype> procedureId)
    {
        if (!prototypes.TryIndex(procedureId, out SurgeryProcedurePrototype? procedure) ||
            !procedure.RequiresTool || procedure.PrimaryTool.IsHand)
            return (null, null);

        string? proper = null;
        if (procedure.PrimaryTool.Tag.HasValue)
        {
            var tagId = procedure.PrimaryTool.Tag.Value.ToString();
            proper = prototypes.TryIndex<SurgicalToolPrototype>(tagId, out var toolProto)
                ? toolProto.DisplayName
                : GetToolTagLocaleKey(tagId);
        }
        else if (procedure.PrimaryTool.DamageType.HasValue)
        {
            proper = procedure.PrimaryTool.DamageType.Value switch
            {
                ImprovisedDamageType.Slash => "health-analyzer-surgery-improvised-slash",
                ImprovisedDamageType.Heat => "health-analyzer-surgery-improvised-heat",
                ImprovisedDamageType.Blunt => "health-analyzer-surgery-improvised-blunt",
                _ => null
            };
        }

        string? improvised = null;
        if (procedure.ImprovisedTools.Count > 0)
        {
            var parts = procedure.ImprovisedTools.Select(t =>
            {
                if (t.Tag.HasValue)
                {
                    var tagId = t.Tag.Value.ToString();
                    return prototypes.TryIndex<SurgicalToolPrototype>(tagId, out var toolProto)
                        ? toolProto.DisplayName
                        : GetToolTagLocaleKey(tagId);
                }
                if (t.DamageType.HasValue)
                {
                    return t.DamageType.Value switch
                    {
                        ImprovisedDamageType.Slash => "health-analyzer-surgery-improvised-slash",
                        ImprovisedDamageType.Heat => "health-analyzer-surgery-improvised-heat",
                        ImprovisedDamageType.Blunt => "health-analyzer-surgery-improvised-blunt",
                        _ => null
                    };
                }
                return null;
            }).Where(x => x != null).ToList();
            improvised = parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        return (proper, improvised);
    }

    private static string GetToolTagLocaleKey(string tagId)
    {
        return tagId switch
        {
            "CuttingTool" => "health-analyzer-surgery-tool-cutting",
            "SurgeryTool" => "health-analyzer-surgery-tool-surgery",
            "ManipulatingTool" => "health-analyzer-surgery-tool-manipulating",
            "SawingTool" => "health-analyzer-surgery-tool-sawing",
            "BluntTool" => "health-analyzer-surgery-tool-blunt",
            "SnippingTool" => "health-analyzer-surgery-tool-snipping",
            "HeatWeapon" => "health-analyzer-surgery-tool-cautery",
            "PryingTool" => "health-analyzer-surgery-tool-retractor",
            "HemostatTool" => "health-analyzer-surgery-tool-hemostat",
            "AnchoringTool" => "health-analyzer-surgery-tool-wrench",
            "Wirecutter" => "health-analyzer-surgery-tool-wirecutter",
            "BoneGelTool" => "health-analyzer-surgery-tool-bone-gel",
            "Screwdriver" => "health-analyzer-surgery-tool-screwdriver",
            _ => tagId
        };
    }
}

/// <summary>
/// Handles surgery request BUI messages. Client: prediction. Server: HealthAnalyzerSystem handles.
/// </summary>
public sealed class SharedHealthAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SharedHealthAnalyzerComponent>(HealthAnalyzerUiKey.Key, subs =>
        {
            subs.Event<SurgeryRequestBuiMessage>(OnSurgeryRequest);
        });
    }

    private void OnSurgeryRequest(Entity<SharedHealthAnalyzerComponent> uid, ref SurgeryRequestBuiMessage args)
    {
        // Server uses HealthAnalyzerSystem; we only run on client for prediction
        if (_net.IsServer)
            return;

        if (uid.Comp.ScannedEntity is not { } target)
            return;

        var targetNet = GetNetEntity(target);
        if (args.Target != targetNet)
            return;

        var targetUid = GetEntity(args.Target);
        var bodyPartUid = GetEntity(args.BodyPart);
        var user = args.Actor;

        var ev = new SurgeryRequestEvent(uid.Owner, user, targetUid, bodyPartUid, args.ProcedureId, args.Layer, args.IsImprovised,
            args.Organ.HasValue ? GetEntity(args.Organ.Value) : null);
        RaiseLocalEvent(targetUid, ref ev);

        if (ev.Valid && ev.UsedImprovisedTool && ev.ToolUsed.HasValue && Exists(ev.ToolUsed.Value))
        {
            _popup.PopupPredicted(
                Loc.GetString("health-analyzer-surgery-begin-improvised", ("tool", Identity.Name(ev.ToolUsed.Value, EntityManager))),
                user, user, PopupType.Small);
        }
        else if (!ev.Valid && ev.RejectReason != null && Exists(user))
        {
            var msgKey = ev.RejectReason switch
            {
                "missing-tool" => "health-analyzer-surgery-error-incorrect-tool",
                "already-done" => "health-analyzer-surgery-error-already-done",
                "layer-not-open" => "health-analyzer-surgery-error-layer-not-open",
                "doafter-failed" => "health-analyzer-surgery-error-doafter-failed",
                "invalid-entity" => "health-analyzer-surgery-error-invalid-entity",
                "body-part-not-in-body" => "health-analyzer-surgery-error-body-part-not-in-body",
                "unknown-step" => "health-analyzer-surgery-error-unknown-step",
                "layer-mismatch" => "health-analyzer-surgery-error-layer-mismatch",
                "invalid-limb-type" => "health-analyzer-surgery-error-invalid-limb-type",
                "unknown-species-or-category" => "health-analyzer-surgery-error-unknown-species-or-category",
                "invalid-body-part" => "health-analyzer-surgery-error-invalid-body-part",
                "cannot-detach-limb" => "health-analyzer-surgery-error-cannot-detach-limb",
                "body-part-detached" => "health-analyzer-surgery-error-body-part-detached",
                "organ-already-in-body" => "health-analyzer-surgery-error-organ-already-in-body",
                "limb-not-in-hand" => "health-analyzer-surgery-error-limb-not-in-hand",
                "organ-not-in-body-part" => "health-analyzer-surgery-error-organ-not-in-body-part",
                "organ-not-in-hand" => "health-analyzer-surgery-error-organ-not-in-hand",
                "body-part-no-container" => "health-analyzer-surgery-error-body-part-no-container",
                "no-slot-for-organ" => "health-analyzer-surgery-error-no-slot-for-organ",
                "slot-filled" => "health-analyzer-surgery-error-slot-filled",
                "slime-cannot-receive-implants" => "health-analyzer-surgery-error-slime-cannot-receive-implants",
                "skeleton-cannot-receive-organs" => "health-analyzer-surgery-error-skeleton-cannot-receive-organs",
                _ => "health-analyzer-surgery-error-invalid-surgical-process"
            };
            var msg = Loc.GetString(msgKey);
            _popup.PopupClient(msg, user, user, PopupType.Medium);
        }
    }
}
