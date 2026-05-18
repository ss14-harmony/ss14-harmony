using System.Numerics;
using Content.Server._Harmony.ImmovableRod.Components;
using Content.Server.Popups;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Harmony.ImmovableRod.Systems;

/// <summary>
/// Harmony-side immovable rod deflection for entities marked with <see cref="RodIndestructibleComponent"/>.
/// </summary>
public sealed class RodIndestructibleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public bool TryBounceRod(EntityUid uid, ref StartCollideEvent args)
    {
        if (!TryComp<RodIndestructibleComponent>(args.OtherEntity, out var rodIndestructible))
            return false;

        var now = _timing.CurTime;

        if (rodIndestructible.LastRod == uid && now < rodIndestructible.BounceBlockedUntil)
            return true;

        var velocity = args.OurBody.LinearVelocity;
        if (!TryGetReflectedVelocity(velocity, args.WorldNormal, rodIndestructible.BounceSpeedMultiplier, out var reflected))
            return false;

        rodIndestructible.LastRod = uid;
        rodIndestructible.BounceBlockedUntil = now + rodIndestructible.BounceCooldown;

        _physics.SetLinearVelocity(uid, reflected, body: args.OurBody);
        _physics.SetAngularVelocity(uid, 0f, body: args.OurBody);

        var xform = Transform(uid);
        _transform.SetLocalRotation(uid, reflected.ToWorldAngle() + MathHelper.PiOver2, xform);

        _popup.PopupCoordinates(
            Loc.GetString("rod-indestructible-bounce-popup", ("rod", uid), ("target", args.OtherEntity)),
            Transform(args.OtherEntity).Coordinates,
            PopupType.LargeCaution);

        return true;
    }

    private static bool TryGetReflectedVelocity(
        Vector2 velocity,
        Vector2 worldNormal,
        float speedMultiplier,
        out Vector2 reflected)
    {
        reflected = Vector2.Zero;

        if (velocity.LengthSquared() <= 0.0001f)
            return false;

        var normal = worldNormal.LengthSquared() > 0.0001f
            ? Vector2.Normalize(worldNormal)
            : -Vector2.Normalize(velocity);

        if (Vector2.Dot(velocity, normal) > 0f)
            normal = -normal;

        reflected = Vector2.Reflect(velocity, normal);

        if (!float.IsFinite(reflected.X) || !float.IsFinite(reflected.Y) || reflected.LengthSquared() <= 0.0001f)
            reflected = -velocity;

        reflected *= speedMultiplier;
        return true;
    }
}
