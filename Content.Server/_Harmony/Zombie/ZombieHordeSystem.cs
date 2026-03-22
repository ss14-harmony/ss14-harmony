using Content.Server.Zombies;
using Content.Shared.Alert;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Zombies;
using Microsoft.EntityFrameworkCore;

namespace Content.Server._Harmony.Zombie;

/// <summary>
/// Handles Zombie Horde Logic
/// </summary>
public sealed class ZombieHordeSystem : EntitySystem
{

    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementmod = default!;
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;



    public override void Initialize()
    {
        base.Initialize();


    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ZombieComponent>();


        while (query.MoveNext(out var uid, out var zombieComponent))
        {
            var lookup = _lookup.GetEntitiesInRange(uid, 10);
            var count = 0;

            foreach (var entity in lookup)
                if (HasComp<ZombieComponent>(entity))
                    count++;

            if (count >= 4)
            {
                _alertsSystem.ShowAlert(uid, zombieComponent.ZombieHordeAlert);
                zombieComponent.ZombieMovementSpeedDebuff = 0.9f;
                _movementmod.RefreshMovementSpeedModifiers(uid);
            }
            else
            {
                _alertsSystem.ClearAlert(uid, zombieComponent.ZombieHordeAlert);
                zombieComponent.ZombieMovementSpeedDebuff = 0.7f;
                _movementmod.RefreshMovementSpeedModifiers(uid);
            }
        }
    }
}
