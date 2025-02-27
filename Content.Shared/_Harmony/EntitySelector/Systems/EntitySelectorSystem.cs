using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Content.Shared._Harmony.EntitySelector.Systems;

/// <summary>
/// Provides an API for using an <see cref="EntitySelector"/>
/// </summary>
public sealed class EntitySelectorSystem : EntitySystem
{
    [Robust.Shared.IoC.Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    [PublicAPI]
    public bool EntityMatches(EntityUid entity, EntitySelector selector)
    {
        EnsureInitialized(selector);

        return selector.Matches(entity);
    }

    [PublicAPI]
    public bool EntityMatchesAny(EntityUid entity, IEnumerable<EntitySelector> selectors)
    {
        foreach (var selector in selectors)
        {
            EnsureInitialized(selector);

            if (selector.Matches(entity))
                return true;
        }

        return false;
    }

    [PublicAPI]
    public IEnumerable<EntityUid> AllMatchingEntities(IEnumerable<EntityUid> entities, EntitySelector selector)
    {
        EnsureInitialized(selector);

        foreach (var entity in entities)
        {
            if (selector.Matches(entity))
                yield return entity;
        }
    }

    [PublicAPI]
    public IEnumerable<EntityUid> AllEntitiesMatchingAny(
        IEnumerable<EntityUid> entities,
        List<EntitySelector> selectors)
    {
        foreach (var entity in entities)
        {
            foreach (var selector in selectors)
            {
                EnsureInitialized(selector);

                if (!selector.Matches(entity))
                    continue;

                yield return entity;
                break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureInitialized(EntitySelector selector)
    {
        if (!selector.Initialized)
            selector.Initialize(_entitySystemManager);
    }
}
