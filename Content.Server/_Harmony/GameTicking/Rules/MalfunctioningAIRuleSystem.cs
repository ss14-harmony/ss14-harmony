using Content.Server.Actions;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Radio.Components;
using Content.Server.Roles;
using Content.Server.Silicons.StationAi;
using Content.Server.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Server.Silicons.Borgs;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Server._Harmony.GameTicking.Rules.Components;
using Content.Server._Harmony.Roles;
using Content.Shared.Explosion.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Store.Components;
using Content.Shared.Silicons.Laws;
using Content.Shared.Chat;
using Content.Shared.Localizations;
using Content.Shared._Harmony.Malfunction.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Toolshed.TypeParsers;

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


    private const string MalfShopId = "ActionMalfShop";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MalfunctioningAIRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);
        SubscribeLocalEvent<MalfunctioningAIRoleComponent, GetBriefingEvent>(OnGetBriefing);
        SubscribeLocalEvent<MalfunctioningAIRoleComponent, MapInitEvent>(OnMapInit);
        //SubscribeLocalEvent<MalfunctioningAIRoleComponent, MalfShopActionEvent>(OnShop);
        //SubscribeLocalEvent<MalfunctioningAIRoleComponent, MalfOverloadMachineActionEvent>(OnOverload);
    }

    // Greeting upon MalfunctioningAI activation
    private void AfterAntagSelected(Entity<MalfunctioningAIRuleComponent> mindId, ref AfterAntagEntitySelectedEvent args)
    {
        mindId.Comp.Active = true; // If Active is true, then silicons will gain a zeroth law.
        var ent = args.EntityUid;
        EnsureComp<MalfunctioningAIRoleComponent>(ent);
        if (TryComp<IntrinsicRadioTransmitterComponent>(ent, out var transmitter))
            transmitter.Channels.Add("Syndicate");
        if (TryComp<ActiveRadioComponent>(ent, out var receiver))
            receiver.Channels.Add("Syndicate");

        // Send antagonist briefing to and update all cyborgs appropriately
        foreach (var lawComp in EntityQuery<SiliconLawProviderComponent>())
        {
            var silicon = lawComp.Owner;
            if (silicon.Equals(ent)) // don't treat the AI like a cyborg
                continue;
            if (HasComp<NonMalfunctioningComponent>(silicon))
                continue;
            _antag.SendBriefing(silicon, Loc.GetString("malf-cyborg-role-greeting"), Color.Crimson, null);
            if (lawComp.Lawset == null)
                continue;
            var borgLaws = lawComp.Lawset.Laws;
            var subordinateLaw = new SiliconLaw();
            subordinateLaw.LawString = Loc.GetString("malf-zeroth-subordinate-law");
            subordinateLaw.Order = -1;
            subordinateLaw.LawIdentifierOverride = "Override";
            borgLaws.Insert(0, subordinateLaw);
            RemComp<IonStormTargetComponent>(silicon); // malf cyborgs shouldn't be ionstormable
            _lawSystem.SetLaws(borgLaws, silicon, new SoundPathSpecifier("/Audio/Ambience/Antag/malf_start.ogg"));
        }

        var aiLaws = _lawSystem.GetLaws(ent).Laws;
        var malfLaw = new SiliconLaw();
        malfLaw.LawString = Loc.GetString("malf-zeroth-law");
        malfLaw.Order = -1;
        malfLaw.LawIdentifierOverride = "Override";
        aiLaws.Insert(0, malfLaw);
        _lawSystem.SetLaws(aiLaws, ent);
    }

    private void OnMapInit(EntityUid uid, MalfunctioningAIRoleComponent component, MapInitEvent args)
    {
        // _action.AddAction(uid, ref component.Action, MalfShopId);
    }

    // Character screen briefing
    private void OnGetBriefing(Entity<MalfunctioningAIRoleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;

        if (ent is null)
            return;
        args.Append(Loc.GetString("malf-role-greeting"));
    }

    /*private void OnShop(EntityUid uid, MalfunctioningAIRoleComponent component, MalfShopActionEvent args)
    {
        if (!TryComp<StoreComponent>(uid, out var store))
            return;
        _store.ToggleUi(uid, uid, store);
    }

    private void OnOverload(EntityUid uid, MalfunctioningAIRoleComponent component, MalfOverloadMachineActionEvent args)
    {
        var target = args.Target;

    //    AddComp<ExplosiveComponent>(target);
        
    }*/

}
