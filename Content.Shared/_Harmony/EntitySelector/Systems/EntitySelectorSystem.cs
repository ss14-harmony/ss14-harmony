using System.Runtime.CompilerServices;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.EntitySelector.Systems;

public sealed class EntitySelectorSystem : EntitySystem
{
    [Robust.Shared.IoC.Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    /// <summary>
    /// Ensures that an <see cref="EntitySelector"/> is correctly initialized.
    /// </summary>
    /// <remarks>
    /// This should always be called before using an <see cref="EntitySelector"/>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureInitialized(EntitySelector selector)
    {
        if (!selector.Initialized)
            selector.Initialize(_entitySystemManager);
    }

    /// <summary>
    /// Get all entities from the enumerator that match the <paramref name="selector"/>
    /// </summary>
    public IEnumerable<EntityUid> GetMatchingEntities(IEnumerable<EntityUid> entities, EntitySelector selector)
    {
        EnsureInitialized(selector);

        foreach (var entity in entities)
        {
            if (selector.Matches(entity))
                yield return entity;
        }
    }
}
