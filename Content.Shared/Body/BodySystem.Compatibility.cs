// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-License-Identifier: MIT

namespace Content.Shared.Body;

public sealed partial class BodySystem
{
    [Obsolete("Use an event-relay based approach instead")]
    public bool TryGetOrgansWithComponent<TComp>(Entity<BodyComponent?> ent, out List<Entity<TComp>> organs) where TComp : Component
    {
        organs = new();
        if (!_bodyQuery.Resolve(ent, ref ent.Comp))
            return false;

        // Funkystation: Changed from ent.comp.organs contained entities. Because the organ structure has changed
        foreach (var organ in GetAllOrgans(ent))
        {
            if (TryComp<TComp>(organ, out var comp))
                organs.Add((organ, comp));
        }

        return organs.Count != 0;
    }
}
