using Content.Shared.Bed.Sleep;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;

namespace Content.Server.Revenant.EntitySystems;

public sealed partial class RevenantSystem
{
    private bool IsHarvestProtected(EntityUid target)
    {
        if (!_mobStateQuery.TryComp(target, out var mobState) || mobState.CurrentState != MobState.Alive)
            return false;

        if (TryComp<MindExaminableComponent>(target, out var mind) && mind.State == MindState.SSD)
            return true;

        return !HasComp<SleepingComponent>(target);
    }
}
