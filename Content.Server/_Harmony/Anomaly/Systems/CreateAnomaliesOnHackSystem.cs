using System.Linq;
using Content.Server._Harmony.Anomaly.Components;
using Content.Server.Anomaly;
using Content.Server.Anomaly.Components;
using Content.Shared._Harmony.Traitor;
using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Content.Shared.Chat;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Server._Harmony.Anomaly.Systems;

public sealed partial class CreateAnomaliesOnHackSystem : EntitySystem
{
    [Dependency] private readonly AnomalySystem _anomaly = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CreateAnomaliesOnHackComponent, StructureHackedEvent>(OnHackStart);
        SubscribeLocalEvent<CreateAnomaliesOnHackComponent, HackUpdateEvent>(OnHackUpdate);
        SubscribeLocalEvent<CreateAnomaliesOnHackComponent, StructureHackCompletedEvent>(OnHackComplete);
    }

    private void OnHackStart(Entity<CreateAnomaliesOnHackComponent> ent, ref StructureHackedEvent args)
    {
        _chat.DispatchStationAnnouncement(ent, Loc.GetString(ent.Comp.InitialAnnouncement, ("time", ent.Comp.HackTime.TotalSeconds)), Loc.GetString(ent.Comp.AnnouncementSender), true, colorOverride: ent.Comp.AnnouncementColor);
    }

    private void OnHackUpdate(Entity<CreateAnomaliesOnHackComponent> ent, ref HackUpdateEvent args)
    {
        args.NextUpdate = _timing.CurTime + ent.Comp.HackTime;

        if (_timing.CurTime - args.Beacon.Comp.TimePlanted >= ent.Comp.HackTime)
            args.CompleteHack = true;
    }

    private void OnHackComplete(Entity<CreateAnomaliesOnHackComponent> ent, ref StructureHackCompletedEvent args)
    {
        if (!TryComp<AnomalyGeneratorComponent>(ent, out var anomalyGenerator))
            return;

        _chat.DispatchStationAnnouncement(ent, Loc.GetString(ent.Comp.FinalAnnouncement), Loc.GetString(ent.Comp.AnnouncementSender), true, colorOverride: ent.Comp.AnnouncementColor);
        var regularAnomQuery = EntityQueryEnumerator<AnomalyComponent>();
        while (regularAnomQuery.MoveNext(out var uid, out _)) // any pre-existing anoms we don't want to affect the severity of
        {
            AddComp<RegularAnomalyComponent>(uid); // mark as regular
        }

        for (int i = 0; i < ent.Comp.Anomalies; i++)
        {
            var gridUid = Transform(ent).GridUid;
            if (gridUid == null)
                continue;

            _anomaly.SpawnOnRandomGridLocation(gridUid.Value, anomalyGenerator.SpawnerPrototype);
        }

        var severeAnomQuery = EntityQueryEnumerator<AnomalyComponent>();
        while (severeAnomQuery.MoveNext(out var uid, out var anom))
        {
            if (HasComp<RegularAnomalyComponent>(uid)) // pre-existing anomaly, do not affect
            {
                RemComp<RegularAnomalyComponent>(uid);
                continue;
            }
            var random = new RobustRandom();
            _anomaly.ChangeAnomalySeverity(uid, Math.Max(random.NextFloat() * ent.Comp.MaxSeverity, ent.Comp.MinSeverity) - anom.Severity); // set it to a value between MinSeverity and MaxSeverity
        }
    }
}
