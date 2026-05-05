using Content.Shared.Silicons.StationAi;

namespace Content.Shared.Mind.Filters;

/// <summary>
/// A mind pool that uses <see cref="SharedStationAiSystem.AddAliveAis"/>.
/// </summary>
public sealed partial class AliveAiPool : IMindPool
{
    void IMindPool.FindMinds(HashSet<Entity<MindComponent>> minds, EntityUid? exclude, IEntityManager entMan, SharedMindSystem mindSys)
    {
        var aiSys = entMan.System<SharedStationAiSystem>();
        aiSys.AddAliveAis(minds, exclude);
    }
}
