using Content.Shared.Examine;
using Content.Shared._Harmony.Malfunction.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Harmony.Malfunction;

public abstract class SharedPendingOverloadSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PendingOverloadComponent, ExaminedEvent>(OnOverloadedExamine);
        SubscribeLocalEvent<PendingOverloadComponent, MalfOverloadMachineActionEvent>(OnOverloadStart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var overloadQuery = EntityQueryEnumerator<PendingOverloadComponent>();
        while (overloadQuery.MoveNext(out var uid, out var overload))
        {
            if (overload.TimeUntilDetonation >= 0)
                overload.TimeUntilDetonation -= frameTime;
            else
            {
                var ev = new MalfOverloadMachineFinishedEvent();
                RaiseLocalEvent(uid, ev);
            }
        }
    }

    private void OnOverloadedExamine(EntityUid uid, PendingOverloadComponent comp, ExaminedEvent args)
    {
        args.PushText(Loc.GetString("malf-machine-overloaded-examine", ("time", Math.Round(comp.TimeUntilDetonation, 1))));
    }

    private void OnOverloadStart(EntityUid uid, PendingOverloadComponent comp, MalfOverloadMachineActionEvent args)
    {
        comp.TimeUntilDetonation = comp.DetonationDuration;
        _audio.PlayPvs(comp.OverloadSound, uid); // alarm to notify anyone nearby it's about to explode
    }
}
