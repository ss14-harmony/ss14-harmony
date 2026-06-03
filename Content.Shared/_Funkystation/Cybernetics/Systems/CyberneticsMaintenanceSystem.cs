using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Cybernetics.Systems;

[UsedImplicitly]
public sealed class CyberneticsMaintenanceSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private TagSystem _tag = default!;

    private const float ScrewdriverDelay = 2f;
    private const float WrenchDelay = 2f;
    private const float WireInsertDelay = 2.5f;

    private static readonly ProtoId<ToolQualityPrototype> ScrewingQuality = "Screwing";
    private static readonly ProtoId<ToolQualityPrototype> AnchoringQuality = "Anchoring";
    private static readonly ProtoId<TagPrototype> PrecisionRepairToolTag = "PrecisionRepairTool";
    private static readonly ProtoId<TagPrototype> CableCoilTag = "CableCoil";
    /// <summary>LV cable coils inherit <c>CableStack</c> stack type; HV/MV use other stack types.</summary>
    private static readonly ProtoId<StackPrototype> LvCableStackTypeId = "Cable";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbComponent, EntGotInsertedIntoContainerMessage>(OnCyberLimbInserted);
        SubscribeLocalEvent<CyberLimbComponent, EntGotRemovedFromContainerMessage>(OnCyberLimbRemoved);

        SubscribeLocalEvent<CyberneticsMaintenanceComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CyberneticsMaintenanceComponent, CyberneticsScrewdriverDoAfterEvent>(OnScrewdriverDoAfter);
        SubscribeLocalEvent<CyberneticsMaintenanceComponent, CyberneticsWrenchDoAfterEvent>(OnWrenchDoAfter);
        SubscribeLocalEvent<CyberneticsMaintenanceComponent, CyberneticsWireInsertDoAfterEvent>(OnWireInsertDoAfter);
    }

    private void OnCyberLimbInserted(Entity<CyberLimbComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        if (!_body.TryGetRootBodyFromOrganContainer(args.Container, out var body))
            return;

        EnsureCyberneticsMaintenanceComponent((body, Comp<BodyComponent>(body)));

        var ev = new CyberLimbAttachedToBodyEvent(body, ent.Owner);
        RaiseLocalEvent(body, ref ev);
    }

    private void OnCyberLimbRemoved(Entity<CyberLimbComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        if (!_body.TryGetRootBodyFromOrganContainer(args.Container, out var body))
            return;

        RecalcCyberneticsMaintenanceComponent((body, Comp<BodyComponent>(body)));

        var ev = new CyberLimbDetachedFromBodyEvent(body, ent.Owner);
        RaiseLocalEvent(body, ref ev);
    }


    private void EnsureCyberneticsMaintenanceComponent(Entity<BodyComponent> body)
    {
        if (HasComp<CyberneticsMaintenanceComponent>(body))
            return;

        var hasCyberLimb = _body.GetAllOrgans(body).Any(o => HasComp<CyberLimbComponent>(o));
        if (hasCyberLimb)
            EnsureComp<CyberneticsMaintenanceComponent>(body);
    }

    private void RecalcCyberneticsMaintenanceComponent(Entity<BodyComponent> body)
    {
        if (!HasComp<CyberneticsMaintenanceComponent>(body))
            return;

        var hasCyberLimb = _body.GetAllOrgans(body).Any(o => HasComp<CyberLimbComponent>(o));
        if (!hasCyberLimb)
            RemComp<CyberneticsMaintenanceComponent>(body);
    }

    private void OnInteractUsing(Entity<CyberneticsMaintenanceComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var comp = ent.Comp;
        var body = ent.Owner;
        var user = args.User;
        var used = args.Used;

        if (_tool.HasQuality(used, ScrewingQuality))
        {
            if (comp.PanelSecured || comp.PanelOpen)
            {
                args.Handled = _tool.UseTool(used, user, body, ScrewdriverDelay, ScrewingQuality, new CyberneticsScrewdriverDoAfterEvent(_tag.HasTag(used, PrecisionRepairToolTag), GetNetEntity(used)));
            }
            return;
        }

        if (_tool.HasQuality(used, AnchoringQuality))
        {
            if (comp.PanelOpen)
            {
                if (!comp.BoltsTight)
                {
                    var cyberCount = _body.GetAllOrgans(body).Count(o => HasComp<CyberLimbComponent>(o));
                    if (comp.WiresInsertedCount < cyberCount)
                    {
                        _popup.PopupClient(Loc.GetString("cyber-maintenance-wires-must-be-replaced"), body, user);
                        return;
                    }
                }
                args.Handled = _tool.UseTool(used, user, body, WrenchDelay, AnchoringQuality, new CyberneticsWrenchDoAfterEvent());
            }
            return;
        }

        if (_tag.HasTag(used, CableCoilTag) && TryComp<StackComponent>(used, out var stack))
        {
            if (!comp.PanelOpen)
            {
                _popup.PopupClient(Loc.GetString("cyber-maintenance-panel-closed"), body, user);
                return;
            }
            if (comp.BoltsTight)
            {
                _popup.PopupClient(Loc.GetString("cyber-maintenance-bolts-must-be-loosened"), body, user);
                return;
            }

            var cyberCount = _body.GetAllOrgans(body).Count(o => HasComp<CyberLimbComponent>(o));
            if (comp.WiresInsertedCount >= cyberCount)
            {
                _popup.PopupClient(Loc.GetString("cyber-maintenance-no-wires-needed"), body, user);
                return;
            }

            if (stack.Count < 1)
            {
                _popup.PopupClient(Loc.GetString("cyber-maintenance-insufficient-wires"), body, user);
                return;
            }

            EntityUid? screwdriver = null;
            foreach (var held in _hands.EnumerateHeld(user))
            {
                if (held == used)
                    continue;
                if (_tool.HasQuality(held, ScrewingQuality))
                {
                    screwdriver = held;
                    break;
                }
            }

            if (screwdriver == null)
            {
                _popup.PopupClient(Loc.GetString("cyber-maintenance-need-screwdriver"), body, user);
                return;
            }

            var isPrecision = _tag.HasTag(screwdriver.Value, PrecisionRepairToolTag);
            var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(WireInsertDelay), new CyberneticsWireInsertDoAfterEvent(isPrecision, GetNetEntity(screwdriver.Value)), body, body, used)
            {
                BreakOnDropItem = true,
                BreakOnMove = true,
                NeedHand = true,
            };

            args.Handled = _doAfter.TryStartDoAfter(doAfterArgs);
        }
    }

    private void OnScrewdriverDoAfter(Entity<CyberneticsMaintenanceComponent> ent, ref CyberneticsScrewdriverDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var comp = ent.Comp;
        var body = ent.Owner;

        if (comp.PanelSecured)
        {
            comp.PanelSecured = false;
            comp.PanelOpen = true;
            // Do not reset BoltsTight - preserve state when resuming after closing panel early
            _popup.PopupPredicted(Loc.GetString("cyber-maintenance-open-panel"), body, args.User);
        }
        else if (comp.PanelOpen)
        {
            comp.PanelSecured = true;
            comp.PanelOpen = false;
            _popup.PopupPredicted(Loc.GetString("cyber-maintenance-lock-panel"), body, args.User);
        }

        if (ResolvePrecisionScrewdriver(args.IsPrecisionRepairTool, args.ToolEntity))
            comp.UnskilledRepairThisSession = false;

        var ev = new CyberMaintenanceStateChangedEvent(body, PanelClosed: comp.PanelSecured);
        RaiseLocalEvent(body, ref ev);
        Dirty(ent, comp);
    }

    private void OnWrenchDoAfter(Entity<CyberneticsMaintenanceComponent> ent, ref CyberneticsWrenchDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var comp = ent.Comp;
        var body = ent.Owner;

        if (!comp.PanelOpen)
            return;

        if (comp.BoltsTight)
        {
            comp.BoltsTight = false;
            _popup.PopupPredicted(Loc.GetString("cyber-maintenance-loosen-bolts"), body, args.User);
            var ev = new CyberMaintenanceStateChangedEvent(body, BoltsLoosened: true);
            RaiseLocalEvent(body, ref ev);
        }
        else
        {
            var cyberCount = _body.GetAllOrgans(body).Count(o => HasComp<CyberLimbComponent>(o));
            if (comp.WiresInsertedCount < cyberCount)
            {
                _popup.PopupClient(Loc.GetString("cyber-maintenance-wires-must-be-replaced"), body, args.User);
                return;
            }

            comp.BoltsTight = true;
            comp.WiresInsertedCount = 0;
            _popup.PopupPredicted(Loc.GetString("cyber-maintenance-tighten-bolts"), body, args.User);
            var ev = new CyberMaintenanceStateChangedEvent(body, RepairCompleted: true);
            RaiseLocalEvent(body, ref ev);
        }

        Dirty(ent, comp);
    }

    private void OnWireInsertDoAfter(Entity<CyberneticsMaintenanceComponent> ent, ref CyberneticsWireInsertDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var comp = ent.Comp;
        var body = ent.Owner;
        var used = args.Used;

        if (!comp.PanelOpen || comp.BoltsTight)
            return;

        var cyberCount = _body.GetAllOrgans(body).Count(o => HasComp<CyberLimbComponent>(o));
        if (comp.WiresInsertedCount >= cyberCount)
        {
            _popup.PopupClient(Loc.GetString("cyber-maintenance-no-wires-needed"), body, args.User);
            return;
        }

        if (used == null || !Exists(used) || !TryComp<StackComponent>(used, out var stack) || stack.Count < 1)
        {
            _popup.PopupClient(Loc.GetString("cyber-maintenance-insufficient-wires"), body, args.User);
            return;
        }

        if (!_stack.TryUse((used.Value, stack), 1))
        {
            _popup.PopupClient(Loc.GetString("cyber-maintenance-insufficient-wires"), body, args.User);
            return;
        }

        comp.WiresInsertedCount++;
        Dirty(ent, comp);

        if (stack.StackTypeId == LvCableStackTypeId
            && !ResolvePrecisionScrewdriver(args.IsPrecisionScrewing, args.ScrewdriverEntity))
        {
            comp.UnskilledRepairThisSession = true;
            Dirty(ent, comp);
        }

        if (comp.WiresInsertedCount >= cyberCount)
        {
            args.Repeat = false;
            _popup.PopupPredicted(Loc.GetString("cyber-maintenance-wires-complete"), body, args.User);
            var ev = new CyberMaintenanceStateChangedEvent(body);
            RaiseLocalEvent(body, ref ev);
        }
        else
        {
            var ev = new CyberMaintenanceStateChangedEvent(body);
            RaiseLocalEvent(body, ref ev);
            args.Repeat = Exists(used) && TryComp<StackComponent>(used, out var s) && s.Count > 0;
        }
    }

    private bool ResolvePrecisionScrewdriver(bool flaggedPrecision, NetEntity? netTool)
    {
        if (flaggedPrecision)
            return true;
        if (netTool is { } net && TryGetEntity(net, out var toolEnt) && _tag.HasTag(toolEnt.Value, PrecisionRepairToolTag))
            return true;
        return false;
    }
}
