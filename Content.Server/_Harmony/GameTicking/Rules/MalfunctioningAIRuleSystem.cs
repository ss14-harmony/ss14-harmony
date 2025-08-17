using Content.Server.Actions;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Radio.Components;
using Content.Server.Roles;
using Content.Server.Silicons.StationAi;
using Content.Server.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Server.Station.Systems;
using Content.Server.Store.Systems;
using Content.Server._Harmony.GameTicking.Rules.Components;
using Content.Server._Harmony.Roles;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared._Harmony.Malfunction;
using Content.Shared._Harmony.Malfunction.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Harmony.GameTicking.Rules;

public sealed class MalfunctioningAIRuleSystem : GameRuleSystem<MalfunctioningAIRuleComponent>
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedStationAiSystem _sharedAi = default!;
    [Dependency] private readonly StationAiSystem _ai = default!;
    [Dependency] private readonly SiliconLawSystem _lawSystem = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private const string MalfShopId = "ActionMalfShop";
    private const string MalfHackApcId = "ActionMalfHackApc";
    private static readonly ProtoId<CurrencyPrototype> CpuCurrencyPrototype = "CPU";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MalfunctioningAIRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);
        SubscribeLocalEvent<MalfunctioningAIRoleComponent, GetBriefingEvent>(OnGetBriefing);
        SubscribeLocalEvent<StoreComponent, MalfShopActionEvent>(OnShop);
        SubscribeLocalEvent<MalfunctioningAIRoleComponent, MalfHackApcActionEvent>(OnApcHacked);
        //SubscribeLocalEvent<MalfunctioningAIRoleComponent, MalfOverloadMachineActionEvent>(OnOverload);
    }

    // Greeting upon MalfunctioningAI activation
    private void AfterAntagSelected(Entity<MalfunctioningAIRuleComponent> mindId, ref AfterAntagEntitySelectedEvent args)
    {
        mindId.Comp.Active = true; // If Active is true, that means there is an active malfunctioning AI. As a result, silicons will gain a zeroth law.
        var ent = args.EntityUid;
        EnsureComp<MalfunctioningAIRoleComponent>(ent);
        if (TryComp<IntrinsicRadioTransmitterComponent>(ent, out var transmitter))
            transmitter.Channels.Add("Syndicate");
        if (TryComp<ActiveRadioComponent>(ent, out var receiver))
            receiver.Channels.Add("Syndicate");

        _action.AddAction(ent, MalfShopId);
        _action.AddAction(ent, MalfHackApcId);

        // Send antagonist briefing to and update all cyborgs appropriately
        foreach (var lawComp in EntityQuery<SiliconLawProviderComponent>())
        {
            var silicon = lawComp.Owner;
            if (HasComp<NonMalfunctioningComponent>(silicon))
                continue;
            if (lawComp.Lawset == null)
                continue;
            _lawSystem.SetLaws(lawComp.Lawset.Laws, silicon, new SoundPathSpecifier("/Audio/_Harmony/Misc/malf_start.ogg"));
        }
    }

    // Character screen briefing
    private void OnGetBriefing(Entity<MalfunctioningAIRoleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;

        if (ent is null)
            return;
        args.Append(Loc.GetString("malf-role-greeting"));
    }

    private void OnShop(EntityUid uid, StoreComponent component, MalfShopActionEvent args)
    {
        _store.ToggleUi(args.Performer, uid, component);
    }

    private void OnApcHacked(EntityUid uid, MalfunctioningAIRoleComponent component, MalfHackApcActionEvent args)
    {
        if (TryComp<ApcComponent>(args.Target, out var apc))
        {
            if (apc.Hacked)
                return;
            _store.TryAddCurrency(new() { { CpuCurrencyPrototype, 20 } }, uid);
            apc.Hacked = true;
            _popup.PopupEntity(Loc.GetString("malf-apc-hacked"), args.Target, PopupType.LargeCaution);
        }
    }
    /*
    private void OnOverload(EntityUid uid, MalfunctioningAIRoleComponent component, MalfOverloadMachineActionEvent args)
    {
        var target = args.Target;

    //    AddComp<ExplosiveComponent>(target);
        
    }*/

}
