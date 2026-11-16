using System.Numerics;
using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.PowerCell;
using Content.Shared.Verbs;
using Robust.Shared.Map;

namespace Content.Shared._Harmony.SignalJammer;

public sealed class SharedSignalJammerSystem : EntitySystem
{
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;

    private const float MinimumCharge = float.Epsilon;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SignalJammerComponent, ItemToggledEvent>(OnToggle);
        SubscribeLocalEvent<SignalJammerComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<SignalJammerComponent, RefreshChargeRateEvent>(OnChargeRate);
        SubscribeLocalEvent<SignalJammerComponent, GetVerbsEvent<Verb>>(OnVerbs);
        SubscribeLocalEvent<SignalJammerComponent, ExaminedEvent>(OnExamine);
    }

    private void OnActivateAttempt(Entity<SignalJammerComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (!_powerCell.HasCharge(ent.Owner, MinimumCharge, args.User, predicted: true))
            args.Cancelled = true;
    }

    private void OnToggle(Entity<SignalJammerComponent> ent, ref ItemToggledEvent args)
    {
        if (args.User == null)
            return;

        var state = Loc.GetString(args.Activated ? "harmony-signal-jammer-on" : "harmony-signal-jammer-off");
        _popup.PopupEntity(Loc.GetString("harmony-signal-jammer-toggle", ("state", state)), args.User.Value, args.User.Value);
    }

    private void OnChargeRate(Entity<SignalJammerComponent> ent, ref RefreshChargeRateEvent args)
    {
        if (_toggle.IsActivated(ent.Owner))
            args.NewChargeRate -= ent.Comp.Settings[ent.Comp.SelectedPowerLevel].Wattage;
    }

    private void OnVerbs(Entity<SignalJammerComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        for (var i = 0; i < ent.Comp.Settings.Length; i++)
        {
            var index = i;
            var setting = ent.Comp.Settings[index];
            args.Verbs.Add(new Verb
            {
                Priority = index,
                Category = VerbCategory.PowerLevel,
                Disabled = ent.Comp.SelectedPowerLevel == index,
                Text = Loc.GetString(setting.Name),
                Act = () =>
                {
                    ent.Comp.SelectedPowerLevel = index;
                    Dirty(ent);
                    _powerCell.SetDrawEnabled(ent.Owner, _toggle.IsActivated(ent.Owner));
                    _popup.PopupEntity(Loc.GetString(setting.Message), user, user);
                },
            });
        }
    }

    private void OnExamine(Entity<SignalJammerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(IsPowered(ent.Owner) ? "harmony-signal-jammer-examine-on" : "harmony-signal-jammer-examine-off"));
        args.PushMarkup(Loc.GetString("harmony-signal-jammer-setting", ("powerLevel", Loc.GetString(ent.Comp.Settings[ent.Comp.SelectedPowerLevel].Name))));
    }

    public bool IsCovered(EntityUid target, SignalJammerEffect effect)
    {
        var xform = Transform(target);
        return IsCovered(_transform.GetWorldPosition(xform), xform.MapID, effect);
    }

    public bool IsDoorRemoteJammed(EntityUid source, EntityUid door)
    {
        return IsCovered(source, SignalJammerEffect.DoorRemote)
            || IsCovered(door, SignalJammerEffect.DoorRemote);
    }

    public bool IsCovered(Vector2 worldPosition, MapId map, SignalJammerEffect effect)
    {
        var query = EntityQueryEnumerator<SignalJammerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var jammer, out var xform))
        {
            if (xform.MapID != map || !IsPowered(uid))
                continue;

            var range = GetRange(jammer.Settings[jammer.SelectedPowerLevel], effect);
            if (Vector2.DistanceSquared(worldPosition, _transform.GetWorldPosition(xform)) <= range * range)
                return true;
        }

        return false;
    }

    public void GetFields(MapId map, SignalJammerEffect effect, List<(Vector2 Position, float Range)> fields)
    {
        fields.Clear();
        var query = EntityQueryEnumerator<SignalJammerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var jammer, out var xform))
        {
            if (xform.MapID != map || !IsPowered(uid))
                continue;

            var range = GetRange(jammer.Settings[jammer.SelectedPowerLevel], effect);
            fields.Add((_transform.GetWorldPosition(xform), range));
        }
    }

    private bool IsPowered(EntityUid uid)
    {
        return _toggle.IsActivated(uid) && _powerCell.HasCharge(uid, MinimumCharge);
    }

    private static float GetRange(SignalJammerComponent.Setting setting, SignalJammerEffect effect)
    {
        return effect switch
        {
            SignalJammerEffect.Camera => setting.CameraRange,
            SignalJammerEffect.AiVision => setting.AiRange,
            SignalJammerEffect.DoorControl => setting.DoorRange,
            SignalJammerEffect.DoorRemote => setting.RemoteRange,
            _ => 0,
        };
    }
}
