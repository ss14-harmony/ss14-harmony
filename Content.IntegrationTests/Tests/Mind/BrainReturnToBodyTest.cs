using Content.IntegrationTests.Pair;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Mind;

[TestFixture]
public sealed class BrainReturnToBodyTest
{
    private static readonly EntProtoId HumanProto = "MobHuman";
    private static readonly EntProtoId MmiProto = "MMI";
    private static readonly ProtoId<DamageTypePrototype> BluntDamage = "Blunt";

    private static EntityUid GetHeadPart(IEntityManager em, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Head") };
        em.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Is.Not.Empty, "Body should have a head part");
        return ev.Parts[0];
    }

    private static EntityUid? FindBrain(IEntityManager em, BodySystem bodySys, EntityUid body)
    {
        foreach (var organ in bodySys.GetAllOrgans(body))
        {
            if (em.HasComponent<BrainComponent>(organ))
                return organ;
        }

        return null;
    }

    private static void KillHuman(
        IEntityManager em,
        DamageableSystem damageable,
        MobThresholdSystem thresholds,
        IPrototypeManager protoMan,
        EntityUid human)
    {
        var dmg = em.GetComponent<DamageableComponent>(human);
        var deathThreshold = thresholds.GetThresholdForState(human, MobState.Dead);
        damageable.SetDamage((human, dmg), new DamageSpecifier(protoMan.Index(BluntDamage), deathThreshold));
    }

    [Test]
    public async Task ReturnToBody_AttachesToBrain_AfterGhostAndBrainRemoval()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true
        });

        var server = pair.Server;
        var em = server.ResolveDependency<IServerEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mindSys = em.System<MindSystem>();
        var ghostSys = em.System<GhostSystem>();
        var bodySys = em.System<BodySystem>();
        var damageable = em.System<DamageableSystem>();
        var thresholds = em.System<MobThresholdSystem>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var map = await pair.CreateTestMap();
        Assert.That(playerMan.Sessions.Length, Is.EqualTo(1));
        var session = playerMan.Sessions[0];

        EntityUid human = default;
        EntityUid mindId = default!;
        MindComponent mind = default!;
        EntityUid brain = default;

        await server.WaitAssertion(() =>
        {
            human = em.SpawnEntity(HumanProto, map.GridCoords);
            mindId = mindSys.CreateMind(session.UserId, "TestMind").Owner;
            mind = em.GetComponent<MindComponent>(mindId);
            mindSys.TransferTo(mindId, human, mind: mind);
            playerMan.SetAttachedEntity(session, human);

            KillHuman(em, damageable, thresholds, protoMan, human);
            Assert.That(em.GetComponent<MobStateComponent>(human).CurrentState, Is.EqualTo(MobState.Dead));

            var brainUid = FindBrain(em, bodySys, human);
            Assert.That(brainUid, Is.Not.Null);
            brain = brainUid.Value;
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(ghostSys.OnGhostAttempt(mindId, canReturnGlobal: true), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var removeEv = new OrganRemoveRequestEvent(brain) { Destination = em.GetComponent<TransformComponent>(human).Coordinates };
            em.EventBus.RaiseLocalEvent(brain, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Brain removal should succeed");

            mind = em.GetComponent<MindComponent>(mindId);
            Assert.That(mind.BrainEntity, Is.EqualTo(em.GetNetEntity(brain)));
            Assert.That(mind.OwnedEntity, Is.EqualTo(brain));
            Assert.That(mind.VisitingEntity, Is.Not.Null);
            Assert.That(em.HasComponent<GhostComponent>(mind.VisitingEntity!.Value));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            mindSys.ReturnToBody(session);
            var mind = em.GetComponent<MindComponent>(mindId);
            Assert.That(session.AttachedEntity, Is.EqualTo(brain));
            Assert.That(mind.VisitingEntity, Is.Null);
            Assert.That(mind.OwnedEntity, Is.EqualTo(brain));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InsertBrainIntoDeadBody_YanksGhostOntoBody()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true
        });

        var server = pair.Server;
        var em = server.ResolveDependency<IServerEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mindSys = em.System<MindSystem>();
        var ghostSys = em.System<GhostSystem>();
        var bodySys = em.System<BodySystem>();
        var damageable = em.System<DamageableSystem>();
        var thresholds = em.System<MobThresholdSystem>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var map = await pair.CreateTestMap();
        Assert.That(playerMan.Sessions.Length, Is.EqualTo(1));
        var session = playerMan.Sessions[0];

        EntityUid donor = default;
        EntityUid recipient = default;
        EntityUid mindId = default!;
        EntityUid donorBrain = default;

        await server.WaitAssertion(() =>
        {
            donor = em.SpawnEntity(HumanProto, map.GridCoords);
            recipient = em.SpawnEntity(HumanProto, map.GridCoords.Offset(new Vector2(2, 0)));

            mindId = mindSys.CreateMind(session.UserId, "Donor").Owner;
            var mind = em.GetComponent<MindComponent>(mindId);
            mindSys.TransferTo(mindId, donor, mind: mind);
            playerMan.SetAttachedEntity(session, donor);

            KillHuman(em, damageable, thresholds, protoMan, donor);
            KillHuman(em, damageable, thresholds, protoMan, recipient);

            var b = FindBrain(em, bodySys, donor);
            Assert.That(b, Is.Not.Null);
            donorBrain = b.Value;
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(ghostSys.OnGhostAttempt(mindId, canReturnGlobal: true), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var coords = em.GetComponent<TransformComponent>(donor).Coordinates;
            var removeDonor = new OrganRemoveRequestEvent(donorBrain) { Destination = coords };
            em.EventBus.RaiseLocalEvent(donorBrain, ref removeDonor);
            Assert.That(removeDonor.Success, Is.True);

            var recipientBrain = FindBrain(em, bodySys, recipient);
            Assert.That(recipientBrain, Is.Not.Null);
            var removeRecipient = new OrganRemoveRequestEvent(recipientBrain.Value) { Destination = coords };
            em.EventBus.RaiseLocalEvent(recipientBrain.Value, ref removeRecipient);
            Assert.That(removeRecipient.Success, Is.True);

            var head = GetHeadPart(em, recipient);
            var insert = new OrganInsertRequestEvent(head, donorBrain);
            em.EventBus.RaiseLocalEvent(head, ref insert);
            Assert.That(insert.Success, Is.True, "Insert donor brain into dead recipient should succeed");
        });

        await pair.RunTicksSync(25);

        await server.WaitAssertion(() =>
        {
            var mind = em.GetComponent<MindComponent>(mindId);
            Assert.That(mind.VisitingEntity, Is.Null);
            Assert.That(mind.OwnedEntity, Is.EqualTo(recipient));
            Assert.That(session.AttachedEntity, Is.EqualTo(recipient));
            Assert.That(em.HasComponent<GhostComponent>(session.AttachedEntity!.Value), Is.False);
            Assert.That(mindSys.ResolveBrainRoot(mind), Is.EqualTo(recipient));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InsertBrainIntoAliveBody_DoesNotYankGhost()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true
        });

        var server = pair.Server;
        var em = server.ResolveDependency<IServerEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mindSys = em.System<MindSystem>();
        var ghostSys = em.System<GhostSystem>();
        var bodySys = em.System<BodySystem>();
        var damageable = em.System<DamageableSystem>();
        var thresholds = em.System<MobThresholdSystem>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var map = await pair.CreateTestMap();
        Assert.That(playerMan.Sessions.Length, Is.EqualTo(1));
        var session = playerMan.Sessions[0];

        EntityUid donor = default;
        EntityUid recipient = default;
        EntityUid mindId = default!;
        EntityUid donorBrain = default;

        await server.WaitAssertion(() =>
        {
            donor = em.SpawnEntity(HumanProto, map.GridCoords);
            recipient = em.SpawnEntity(HumanProto, map.GridCoords.Offset(new Vector2(2, 0)));

            mindId = mindSys.CreateMind(session.UserId, "DonorAlive").Owner;
            var mind = em.GetComponent<MindComponent>(mindId);
            mindSys.TransferTo(mindId, donor, mind: mind);
            playerMan.SetAttachedEntity(session, donor);

            KillHuman(em, damageable, thresholds, protoMan, donor);
            // Recipient stays alive (no kill)

            var b = FindBrain(em, bodySys, donor);
            Assert.That(b, Is.Not.Null);
            donorBrain = b.Value;
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(ghostSys.OnGhostAttempt(mindId, canReturnGlobal: true), Is.True);
        });

        await pair.RunTicksSync(5);

        EntityUid? ghostUid = null;

        await server.WaitAssertion(() =>
        {
            ghostUid = em.GetComponent<MindComponent>(mindId).VisitingEntity;
            Assert.That(ghostUid, Is.Not.Null);

            var coords = em.GetComponent<TransformComponent>(donor).Coordinates;
            var removeDonor = new OrganRemoveRequestEvent(donorBrain) { Destination = coords };
            em.EventBus.RaiseLocalEvent(donorBrain, ref removeDonor);
            Assert.That(removeDonor.Success, Is.True);

            var recipientBrain = FindBrain(em, bodySys, recipient);
            Assert.That(recipientBrain, Is.Not.Null);
            var removeRecipient = new OrganRemoveRequestEvent(recipientBrain.Value) { Destination = coords };
            em.EventBus.RaiseLocalEvent(recipientBrain.Value, ref removeRecipient);
            Assert.That(removeRecipient.Success, Is.True);

            var head = GetHeadPart(em, recipient);
            var insert = new OrganInsertRequestEvent(head, donorBrain);
            em.EventBus.RaiseLocalEvent(head, ref insert);
            Assert.That(insert.Success, Is.True, "Insert into alive recipient should succeed");
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var mind = em.GetComponent<MindComponent>(mindId);
            Assert.That(mind.VisitingEntity, Is.EqualTo(ghostUid));
            Assert.That(mind.OwnedEntity, Is.EqualTo(recipient));
            Assert.That(session.AttachedEntity, Is.EqualTo(ghostUid));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MmiInsert_YanksGhost_MmiEject_KeepsGhostUntilReturn()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true
        });

        var server = pair.Server;
        var em = server.ResolveDependency<IServerEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mindSys = em.System<MindSystem>();
        var ghostSys = em.System<GhostSystem>();
        var bodySys = em.System<BodySystem>();
        var itemSlots = em.System<ItemSlotsSystem>();
        var damageable = em.System<DamageableSystem>();
        var thresholds = em.System<MobThresholdSystem>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var map = await pair.CreateTestMap();
        Assert.That(playerMan.Sessions.Length, Is.EqualTo(1));
        var session = playerMan.Sessions[0];

        EntityUid human = default;
        EntityUid mmi = default;
        EntityUid mindId = default!;
        EntityUid brain = default;

        await server.WaitAssertion(() =>
        {
            human = em.SpawnEntity(HumanProto, map.GridCoords);
            mmi = em.SpawnEntity(MmiProto, map.GridCoords.Offset(new Vector2(1, 0)));

            mindId = mindSys.CreateMind(session.UserId, "MmiTest").Owner;
            var mind = em.GetComponent<MindComponent>(mindId);
            mindSys.TransferTo(mindId, human, mind: mind);
            playerMan.SetAttachedEntity(session, human);

            KillHuman(em, damageable, thresholds, protoMan, human);

            var b = FindBrain(em, bodySys, human);
            Assert.That(b, Is.Not.Null);
            brain = b.Value;
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(ghostSys.OnGhostAttempt(mindId, canReturnGlobal: true), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var coords = em.GetComponent<TransformComponent>(human).Coordinates;
            var removeEv = new OrganRemoveRequestEvent(brain) { Destination = coords };
            em.EventBus.RaiseLocalEvent(brain, ref removeEv);
            Assert.That(removeEv.Success, Is.True);

            var mmiComp = em.GetComponent<MMIComponent>(mmi);
            Assert.That(itemSlots.TryInsert(mmi, mmiComp.BrainSlotId, brain, user: null), Is.True);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var mind = em.GetComponent<MindComponent>(mindId);
            Assert.That(mind.OwnedEntity, Is.EqualTo(mmi));
            Assert.That(session.AttachedEntity, Is.EqualTo(mmi));
            Assert.That(mind.VisitingEntity, Is.Null);
        });

        await server.WaitAssertion(() =>
        {
            var mmiComp = em.GetComponent<MMIComponent>(mmi);
            Assert.That(itemSlots.TryEject(mmi, mmiComp.BrainSlotId, user: null, out _), Is.True);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var mind = em.GetComponent<MindComponent>(mindId);
            // Eject uses ghostCheckOverride: false — no visit; player controls the brain directly.
            Assert.That(mind.VisitingEntity, Is.Null);
            Assert.That(mind.OwnedEntity, Is.EqualTo(brain));
            Assert.That(session.AttachedEntity, Is.EqualTo(brain));
            Assert.That(mindSys.ResolveBrainRoot(mind), Is.EqualTo(brain));
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(ghostSys.OnGhostAttempt(mindId, canReturnGlobal: true), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var mind = em.GetComponent<MindComponent>(mindId);
            Assert.That(mind.VisitingEntity, Is.Not.Null);
            Assert.That(mind.OwnedEntity, Is.EqualTo(brain));
            mindSys.ReturnToBody(session);
            mind = em.GetComponent<MindComponent>(mindId);
            Assert.That(session.AttachedEntity, Is.EqualTo(brain));
            Assert.That(mind.VisitingEntity, Is.Null);
        });

        await pair.CleanReturnAsync();
    }
}
