using System.Linq;
using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(SurgeryProcedurePrototype))]
[TestOf(typeof(SurgicalToolPrototype))]
public sealed class SurgeryToolsValidationIntegrationTest
{
    [Test]
    public async Task SurgeryProcedures_HaveValidToolConfig()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var prototypes = server.ResolveDependency<IPrototypeManager>();

        foreach (var procedure in prototypes.EnumeratePrototypes<SurgeryProcedurePrototype>())
        {
            if (procedure.RequiresTool)
            {
                Assert.That(procedure.PrimaryTool.IsHand, Is.False,
                    $"{procedure.ID}: RequiresTool=true but PrimaryTool.IsHand is true");
                Assert.That(procedure.PrimaryTool.Tag.HasValue || procedure.PrimaryTool.DamageType.HasValue, Is.True,
                    $"{procedure.ID}: RequiresTool=true but PrimaryTool has neither Tag nor DamageType");
            }
            else
            {
                Assert.That(procedure.PrimaryTool.IsHand, Is.True,
                    $"{procedure.ID}: RequiresTool=false but PrimaryTool.IsHand is false");
            }

            if (procedure.PrimaryTool.Tag.HasValue)
            {
                var tagStr = procedure.PrimaryTool.Tag.Value.ToString();
                var hasSurgicalTool = prototypes.EnumeratePrototypes<SurgicalToolPrototype>()
                    .Any(st => st.Tag.ToString() == tagStr || st.ID == tagStr);
                Assert.That(hasSurgicalTool, Is.True,
                    $"{procedure.ID}: PrimaryTool tag {tagStr} has no SurgicalToolPrototype");
            }

            foreach (var improvised in procedure.ImprovisedTools)
            {
                if (improvised.Tag.HasValue)
                {
                    var tagExists = prototypes.TryIndex(improvised.Tag.Value, out _);
                    Assert.That(tagExists, Is.True,
                        $"{procedure.ID}: ImprovisedTools tag {improvised.Tag.Value} does not exist");
                }
            }
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SurgicalToolPrototypes_HaveValidDisplayName()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var prototypes = server.ResolveDependency<IPrototypeManager>();

        foreach (var proto in prototypes.EnumeratePrototypes<SurgicalToolPrototype>())
        {
            Assert.That(proto.DisplayName.Id, Is.Not.Empty,
                $"{proto.ID}: SurgicalToolPrototype must have non-empty DisplayName");
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BodyPartSurgerySteps_HaveValidToolConfigInStepEntries()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var prototypes = server.ResolveDependency<IPrototypeManager>();

        foreach (var proto in prototypes.EnumeratePrototypes<BodyPartSurgeryStepsPrototype>())
        {
            if (proto.OrganOnlyPreset.HasValue)
                continue;

            foreach (var entry in proto.SkinOpenStepEntries.Concat(proto.SkinCloseStepEntries)
                         .Concat(proto.TissueOpenStepEntries).Concat(proto.TissueCloseStepEntries))
            {
                var hasPrimary = entry.PrimaryTag.HasValue || entry.PrimaryDamageType.HasValue;
                if (!hasPrimary && prototypes.TryIndex(entry.Procedure, out SurgeryProcedurePrototype? proc) && proc != null)
                {
                    hasPrimary = proc.PrimaryTool.Tag.HasValue || proc.PrimaryTool.DamageType.HasValue
                        || proc.PrimaryTool.IsHand;
                }
                Assert.That(hasPrimary, Is.True,
                    $"{proto.ID}: Step entry for procedure {entry.Procedure} has no primary tool (from entry or procedure)");
            }
        }

        await pair.CleanReturnAsync();
    }
}
