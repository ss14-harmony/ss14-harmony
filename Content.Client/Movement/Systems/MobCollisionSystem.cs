using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Movement.Systems;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client.Movement.Systems;

public sealed partial class MobCollisionSystem : SharedMobCollisionSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override void Update(float frameTime)
    {
        if (!CfgManager.GetCVar(CCVars.MovementMobPushing))
            return;

        if (_timing.IsFirstTimePredicted)
        {
            var player = _player.LocalEntity;

            if (MobQuery.TryComp(player, out var comp) && PhysicsQuery.TryComp(player, out var physics) && FixturesQuery.TryComp(player, out var fixtures)) // Harmony Addition | Enable Mob Collision: Added FixtureQuery
            {
                //HandleCollisions((player.Value, comp, physics), frameTime); // Harmony Addition | Enable Mob Collision: Added fixtures
                HandleHarmonyCollisions((player.Value, comp, physics, fixtures), frameTime);
            }
        }

        base.Update(frameTime);
    }

    protected override void RaiseCollisionEvent(EntityUid uid, Vector2 direction, float speedMod)
    {
        RaisePredictiveEvent(new MobCollisionMessage()
        {
            Direction = direction,
            SpeedModifier = speedMod,
        });
    }
}
