using Content.IntegrationTests;
using Content.Server.Medical;
using Content.Shared.Body;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Surgery UI is gated when the analyzer reports no operable body parts (empty body part list).
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class SurgeryAvailabilityIntegrationTest
{
    [Test]
    public async Task EntityWithoutBody_SurgeryUnsupported_OnAnalyzerState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var analyzerSys = entityManager.System<HealthAnalyzerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            // MobLizard inherits BaseMobAnimal and has flat body_organs — it *does* support surgery lists.
            // Use any mob with analyzable damage, then strip Body to model critters with no surgical anatomy.
            var lizard = entityManager.SpawnEntity("MobLizard", mapData.GridCoords);
            Assert.That(entityManager.HasComponent<BodyComponent>(lizard), Is.True);
            entityManager.RemoveComponent<BodyComponent>(lizard);

            var state = analyzerSys.GetHealthAnalyzerUiState(lizard);
            Assert.That(state.SurgerySupported, Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
