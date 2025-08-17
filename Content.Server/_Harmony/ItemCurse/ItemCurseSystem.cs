using Content.Shared._Harmony.ItemCurse;
using Content.Server.Lightning;
using Content.Server.Electrocution;
using Content.Server.Chat.Systems;
using Content.Shared.Popups;

namespace Content.Server._Harmony.ItemCurse;

/// <inheritdoc />
public sealed class ItemCurseSystem : SharedItemCurseSystem
{
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;

    // The curse system is mostly handled by SharedItemCurseSystem, this is just overrides for anything which must happen server-side.

    protected override void CreateLightning(EntityUid ent, ItemCurseComponent comp)
    {
        _popups.PopupEntity(Loc.GetString("item-curse-item-activates", ("item", ent)), ent, PopupType.MediumCaution);
        _lightning.ShootRandomLightnings(ent, comp.LightningRange, comp.LightningCount, comp.LightningPrototype);
    }

    protected override void ShockHolder(EntityUid ent, EntityUid source, ItemCurseComponent comp)
    {
        _electrocution.TryDoElectrocution(ent, source, comp.ShockDamage, comp.ShockDuration, true, ignoreInsulation: true);
    }

    protected override void Snap(EntityUid ent, ItemCurseComponent comp)
    {
        _chat.TryEmoteWithChat(ent, comp.SpellUseEmote);
    }
}
