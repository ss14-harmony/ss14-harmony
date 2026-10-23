using System.Linq;
using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMobCollisionSystem
{
    /// <remarks>
    /// Used in place of the NonHarmony version if something changes in the NonHarmony version of this method it's probably best to switch back to that one
    /// </remarks>
    protected bool HandleHarmonyCollisions(Entity<MobCollisionComponent, PhysicsComponent, FixturesComponent> entity, float frameTime)
    {
        var physics = entity.Comp2;

        var mobFixtures = entity.Comp3.Fixtures.First().Value;

        foreach (var fixture in entity.Comp3.Fixtures)
        {
            if (fixture.Key == entity.Comp1.FixtureId)
            {
                mobFixtures = fixture.Value;
            }
        }

        if (physics.ContactCount == 0)
            return false;

        var ourVelocity = entity.Comp2.LinearVelocity;

        if (ourVelocity == Vector2.Zero && !CfgManager.GetCVar(CCVars.MovementPushingStatic))
            return false;

        var xform = Transform(entity.Owner);

        if (xform.ParentUid != xform.GridUid && xform.ParentUid != xform.MapUid)
            return false;

        var ev = new AttemptMobCollideEvent();

        RaiseLocalEvent(entity.Owner, ref ev);

        if (ev.Cancelled)
            return false;

        var (worldPos, worldRot) = _xformSystem.GetWorldPositionRotation(xform);
        var ourTransform = new Transform(worldPos, worldRot);
        var contacts = Physics.GetContacts(entity.Owner);
        var direction = Vector2.Zero;
        var contactCount = 0;
        var ourMass = physics.FixturesMass;
        var speedMod = 1f;

        while (contacts.MoveNext(out var contact))
        {
            if (!contact.IsTouching)
                continue;

            var ourFixture = contact.OurFixture(entity.Owner);

            if (ourFixture.Id != entity.Comp1.FixtureId)
                continue;

            var other = contact.OtherEnt(entity.Owner);

            if (!MobQuery.TryComp(other, out var otherComp) || !PhysicsQuery.TryComp(other, out var otherPhysics) || !FixturesQuery.TryComp(other, out var otherFixtures)) // Harmony addition: Added FixtureQuery
                continue;

            var otherMobFixtures = otherFixtures.Fixtures.First().Value;

            foreach (var fixture in otherFixtures.Fixtures)
            {
                if (fixture.Key == entity.Comp1.FixtureId)
                {
                    otherMobFixtures = fixture.Value;
                }
            }

            // If the we're both blocked by airlocks OR we're both NOT blocked by them
            if (((otherMobFixtures.CollisionMask & (int)CollisionGroup.HumanoidBlockLayer) == (int)CollisionGroup.HumanoidBlockLayer) ^ ((mobFixtures.CollisionMask & (int)CollisionGroup.HumanoidBlockLayer) == (int)CollisionGroup.HumanoidBlockLayer))
                continue;

            var velocityProduct = Vector2.Dot(ourVelocity, otherPhysics.LinearVelocity);

            // If we're moving opposite directions for example then ignore (based on cvar).
            if (velocityProduct < _pushingDotProduct)
            {
                continue;
            }

            var targetEv = new AttemptMobTargetCollideEvent();
            RaiseLocalEvent(other, ref targetEv);

            if (targetEv.Cancelled)
                continue;

            // TODO: More robust overlap detection.
            var otherTransform = Physics.GetPhysicsTransform(other);
            var diff = ourTransform.Position - otherTransform.Position;

            if (diff == Vector2.Zero)
            {
                diff = _random.NextVector2(0.01f);
            }

            // 0.7 for 0.35 + 0.35 for mob bounds (see TODO above).
            // Clamp so we don't get a heap of penetration depth and suddenly lurch other mobs.
            // This is also so we don't have to trigger the speed-cap above.
            // Maybe we just do speedcap and dump this? Though it's less configurable and the cap is just there for cheaters.
            var penDepth = Math.Clamp(0.7f - diff.Length(), 0f, _penCap);

            // Sum the strengths so we get pushes back the same amount (impulse-wise, ignoring prediction).
            var mobMovement = penDepth * diff.Normalized() * (entity.Comp1.Strength + otherComp.Strength);

            // Big mob push smaller mob, needs fine-tuning and potentially another co-efficient.
            if (_massDiffCap > 0f)
            {
                var modifier = Math.Clamp(
                    otherPhysics.FixturesMass / ourMass,
                    1f / _massDiffCap,
                    _massDiffCap);

                mobMovement *= modifier;

                var speedReduction = 1f - entity.Comp1.MinimumSpeedModifier;
                speedReduction /= _penCap / penDepth;
                var speedModifier = Math.Clamp(
                    1f - speedReduction * modifier,
                    entity.Comp1.MinimumSpeedModifier, 1f);

                speedMod = MathF.Min(speedModifier, 1f);
            }

            // Need the push strength proportional to penetration depth.
            direction += mobMovement;
            contactCount++;
        }

        if (direction == Vector2.Zero)
        {
            return contactCount > 0;
        }

        direction *= frameTime;
        RaiseCollisionEvent(entity.Owner, direction, speedMod);
        return true;
    }
}
