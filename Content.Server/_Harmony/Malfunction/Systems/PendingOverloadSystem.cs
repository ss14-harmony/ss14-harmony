using Content.Server.Explosion.EntitySystems;
using Content.Shared._Harmony.Malfunction;
using Content.Shared._Harmony.Malfunction.Components;

namespace Content.Server._Harmony.Malfunction.Systems;

public sealed class PendingOverloadSystem : SharedPendingOverloadSystem
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PendingOverloadComponent, MalfOverloadMachineFinishedEvent>(OnOverloadFinished);
    }

    private void OnOverloadFinished(EntityUid uid, PendingOverloadComponent component, MalfOverloadMachineFinishedEvent args)
    {
        _explosion.QueueExplosion(uid, component.ExplosionType, component.TotalIntensity, component.Slope, component.MaxTileIntensity);
        RemComp<PendingOverloadComponent>(uid);
    }
}
