using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Content.Shared._Harmony.EntitySelector;

/// <summary>
/// Provides an API for using an <see cref="EntitySelector"/>
/// </summary>
public sealed class EntitySelectorManager
{
    [PublicAPI]
    public static bool EntityMatches(EntityUid entity, EntitySelector selector)
    {
        EnsureInitialized(selector);

        return selector.Matches(entity);
    }

    [PublicAPI]
    public static bool EntityMatchesAny(EntityUid entity, IEnumerable<EntitySelector> selectors)
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
    public static IEnumerable<EntityUid> AllMatchingEntities(IEnumerable<EntityUid> entities, EntitySelector selector)
    {
        EnsureInitialized(selector);

        foreach (var entity in entities)
        {
            if (selector.Matches(entity))
                yield return entity;
        }
    }

    [PublicAPI]
    public static IEnumerable<EntityUid> AllEntitiesMatchingAny(
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
    private static void EnsureInitialized(EntitySelector selector)
    {
        if (!selector.Initialized)
            selector.Initialize();
    }
}
