using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cybernetics.Systems;

/// <summary>
/// Shared helpers for cyber arm storage (items in cyber limb arms).
/// </summary>
public sealed class SharedCyberArmStorageSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;

    private static readonly ProtoId<OrganCategoryPrototype> ArmLeft = "ArmLeft";
    private static readonly ProtoId<OrganCategoryPrototype> ArmRight = "ArmRight";

    /// <summary>
    /// Returns all items in cyber arm storage for the given body.
    /// </summary>
    /// <param name="body">The body to get items from.</param>
    /// <param name="armCategory">If specified, only returns items from the arm matching this category (ArmLeft or ArmRight).</param>
    public IEnumerable<(EntityUid Limb, EntityUid Item)> GetCyberArmStorageItems(EntityUid body, ProtoId<OrganCategoryPrototype>? armCategory = null)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Organs == null)
            yield break;

        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (!HasComp<CyberLimbComponent>(organ))
                continue;

            if (!TryComp<OrganComponent>(organ, out var organComp))
                continue;

            if (organComp.Category != ArmLeft && organComp.Category != ArmRight)
                continue;

            if (armCategory != null && organComp.Category != armCategory)
                continue;

            if (!TryComp<StorageComponent>(organ, out var storage) || storage.Container == null)
                continue;

            foreach (var item in storage.Container.ContainedEntities)
            {
                yield return (organ, item);
            }
        }
    }

    /// <summary>
    /// Returns true if the entity is in the body's cyber arm storage.
    /// </summary>
    public bool IsInCyberArmStorage(EntityUid entity, EntityUid body)
    {
        foreach (var (_, item) in GetCyberArmStorageItems(body))
        {
            if (item == entity)
                return true;
        }
        return false;
    }
}
