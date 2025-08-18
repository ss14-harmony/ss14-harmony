
using Content.Shared.Audio;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station.Components;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared._Harmony.Malfunction;
using Content.Shared._Harmony.Malfunction.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Examine;

namespace Content.Shared._Harmony.Malfunction;

public sealed class SharedMalfunctioningAISystem : EntitySystem
{
    [Dependency] private readonly SharedStationAiSystem _sharedAi = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PendingOverloadComponent, ExaminedEvent>(OnOverloadedExamine);
    }

    private void OnOverloadedExamine(EntityUid uid, PendingOverloadComponent comp, ExaminedEvent args)
    {
        args.PushText(Loc.GetString("malf-machine-overloaded-examine", ("time", Math.Round(comp.TimeUntilDetonation, 1))));
    }
}
