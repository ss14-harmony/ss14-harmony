using Content.Server.Actions;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Radio.Components;
using Content.Server.Roles;
using Content.Server.Silicons.Laws;
using Content.Server.Store.Systems;
using Content.Server._Harmony.GameTicking.Rules.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station.Components;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Verbs;
using Content.Shared._Harmony.Malfunction;
using Content.Shared._Harmony.Malfunction.Components;
using Content.Shared._Harmony.Roles.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Harmony.GameTicking.Rules;

public sealed class MalfunctioningAIRuleSystem : GameRuleSystem<MalfunctioningAIRuleComponent>
{
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly SiliconLawSystem _lawSystem = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ApcSystem _apc = default!;

    private const string MalfShopId = "ActionMalfShop";
    private static readonly ProtoId<CurrencyPrototype> CpuCurrencyPrototype = "CPU";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MalfunctioningAIRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);
        SubscribeLocalEvent<MalfunctioningAIRoleComponent, GetBriefingEvent>(OnGetBriefing);
        SubscribeLocalEvent<StoreComponent, MalfShopActionEvent>(OnShop);
        SubscribeLocalEvent<ApcComponent, GetVerbsEvent<AlternativeVerb>>(OnApcVerbs);
        SubscribeLocalEvent<MalfDoomsdayActivatedEvent>(OnDoomsdayActivated);
    }

    public bool IsAIDeactivated(EntityUid uid)
    {
        return HasComp<IntellicardedComponent>(uid) || !HasComp<StationMemberComponent>(Transform(uid).GridUid);
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
        if (IsAIDeactivated(uid)) return;
        if (args.Handled) return;
        _store.ToggleUi(args.Performer, uid, component);
        args.Handled = true;
    }

    public override void Update(float frameTime) // timers being held on components get ticked down
    {
        base.Update(frameTime);

        var malfQuery = EntityQueryEnumerator<MalfunctioningAIRoleComponent>();
        while (malfQuery.MoveNext(out var uid, out var malf))
        {
            if (malf.CurrentHackCooldown >= 0)
                malf.CurrentHackCooldown -= frameTime;
        }
    }

    private void OnApcVerbs(EntityUid uid, ApcComponent apc, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp<MalfunctioningAIRoleComponent>(args.User, out var malfComp)
        || !args.CanComplexInteract
        || !args.CanInteract) return;
        if (apc.Hacked) return;
        if (!TryComp<StationAiWhitelistComponent>(args.Target, out var whitelist) || !whitelist.Enabled) return;

        var verb = new AlternativeVerb
        {
            Text = malfComp.CurrentHackCooldown >= 0 ? Loc.GetString("malf-hack-verb-cooldown", ("time", Math.Ceiling(malfComp.CurrentHackCooldown))) : Loc.GetString("malf-hack-verb"),
            Act = () =>
            {
                if (malfComp.CurrentHackCooldown >= 0) return;
                _store.TryAddCurrency(new() { { CpuCurrencyPrototype, 10 } }, args.User);
                apc.Hacked = true;
                _popup.PopupEntity(Loc.GetString("malf-apc-hacked"), args.Target, PopupType.MediumCaution);

                _apc.UpdateApcState(uid, apc);
                _audio.PlayPvs(malfComp.HackSound, uid);
                malfComp.CurrentHackCooldown = malfComp.HackApcTime;
            }
        };
        args.Verbs.Add(verb);
    }

    private void OnDoomsdayActivated(MalfDoomsdayActivatedEvent args)
    {
        var query = EntityQueryEnumerator<MalfunctioningAIRuleComponent>();
        while (query.MoveNext(out var comp))
            comp.DoomsdayActivated = true;
    }
}
