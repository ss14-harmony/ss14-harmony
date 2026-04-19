using System.Linq;
using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(BodyPartSurgeryStepsPrototype))]
public sealed class SurgeryStepsValidationIntegrationTest
{
    [Test]
    public async Task BodyPartSurgerySteps_HasMinTwoStepsAndPairedRepair()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var prototypes = server.ResolveDependency<IPrototypeManager>();

        foreach (var proto in prototypes.EnumeratePrototypes<BodyPartSurgeryStepsPrototype>())
        {
            if (proto.OrganOnlyPreset.HasValue)
                continue;

            var skinOpen = proto.GetSkinOpenStepIds(prototypes);
            var skinClose = proto.GetSkinCloseStepIds(prototypes);

            Assert.That(skinOpen.Count, Is.GreaterThanOrEqualTo(2),
                $"{proto.ID}: SkinOpenSteps must have at least 2 steps");
            Assert.That(skinClose.Count, Is.GreaterThanOrEqualTo(2),
                $"{proto.ID}: SkinCloseSteps must have at least 2 steps");

            foreach (var openStepId in skinOpen)
            {
                var openProcId = BodyPartSurgeryStepsPrototype.GetProcedureForStep(openStepId);
                var hasPairedClose = skinClose.Any(closeStepId =>
                {
                    var closeProcId = BodyPartSurgeryStepsPrototype.GetProcedureForStep(closeStepId);
                    return prototypes.TryIndex<SurgeryProcedurePrototype>(closeProcId, out var closeProc)
                        && closeProc.UndoesProcedure?.ToString() == openProcId;
                });
                Assert.That(hasPairedClose, Is.True,
                    $"{proto.ID}: SkinOpen step {openStepId} (procedure {openProcId}) has no paired close step");
            }

            foreach (var closeStepId in skinClose)
            {
                var closeProcId = BodyPartSurgeryStepsPrototype.GetProcedureForStep(closeStepId);
                Assert.That(prototypes.TryIndex<SurgeryProcedurePrototype>(closeProcId, out var closeProc), Is.True,
                    $"{proto.ID}: Close step {closeStepId} procedure not found");
                Assert.That(closeProc.UndoesProcedure, Is.Not.Null,
                    $"{proto.ID}: SkinClose step {closeStepId} must have UndoesProcedure");
                var undoesProcId = closeProc.UndoesProcedure!.Value.ToString();
                Assert.That(skinOpen.Any(openStepId =>
                    BodyPartSurgeryStepsPrototype.GetProcedureForStep(openStepId) == undoesProcId), Is.True,
                    $"{proto.ID}: SkinClose step {closeStepId} UndoesProcedure {undoesProcId} not in SkinOpenSteps");
            }

            var tissueOpen = proto.GetTissueOpenStepIds(prototypes);
            var tissueClose = proto.GetTissueCloseStepIds(prototypes);

            if (tissueOpen.Count > 0)
            {
                Assert.That(tissueOpen.Count, Is.GreaterThanOrEqualTo(2),
                    $"{proto.ID}: TissueOpenSteps must have at least 2 steps when non-empty");
                Assert.That(tissueClose.Count, Is.GreaterThanOrEqualTo(2),
                    $"{proto.ID}: TissueCloseSteps must have at least 2 steps when tissue open is non-empty");

                foreach (var openStepId in tissueOpen)
                {
                    var openProcId = BodyPartSurgeryStepsPrototype.GetProcedureForStep(openStepId);
                    var hasPairedClose = tissueClose.Any(closeStepId =>
                    {
                        var closeProcId = BodyPartSurgeryStepsPrototype.GetProcedureForStep(closeStepId);
                        return prototypes.TryIndex<SurgeryProcedurePrototype>(closeProcId, out var closeProc)
                            && closeProc.UndoesProcedure?.ToString() == openProcId;
                    });
                    Assert.That(hasPairedClose, Is.True,
                        $"{proto.ID}: TissueOpen step {openStepId} (procedure {openProcId}) has no paired close step");
                }

                foreach (var closeStepId in tissueClose)
                {
                    var closeProcId = BodyPartSurgeryStepsPrototype.GetProcedureForStep(closeStepId);
                    Assert.That(prototypes.TryIndex<SurgeryProcedurePrototype>(closeProcId, out var closeProc), Is.True,
                        $"{proto.ID}: Tissue close step {closeStepId} procedure not found");
                    Assert.That(closeProc.UndoesProcedure, Is.Not.Null,
                        $"{proto.ID}: TissueClose step {closeStepId} must have UndoesProcedure");
                    var undoesProcId = closeProc.UndoesProcedure!.Value.ToString();
                    Assert.That(tissueOpen.Any(openStepId =>
                        BodyPartSurgeryStepsPrototype.GetProcedureForStep(openStepId) == undoesProcId), Is.True,
                        $"{proto.ID}: TissueClose step {closeStepId} UndoesProcedure {undoesProcId} not in TissueOpenSteps");
                }
            }
        }

        await pair.CleanReturnAsync();
    }
}
