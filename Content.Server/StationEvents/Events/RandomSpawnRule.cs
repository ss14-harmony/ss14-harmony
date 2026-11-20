using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
// Harmony Change Start - Moffstation Dead Drop Port
using Content.Server.Radio.EntitySystems;
using Content.Server.Pinpointer;
using Robust.Shared.Utility;
// Harmony Change End

namespace Content.Server.StationEvents.Events;

public sealed partial class RandomSpawnRule : StationEventSystem<RandomSpawnRuleComponent>
{
    // Harmony Change Start - Moffstation Dead Drop Port
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private RadioSystem _radio = default!;
    // Harmony Change End

    protected override void Started(EntityUid uid, RandomSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (TryFindRandomTile(out _, out _, out _, out var coords))
        {
            Sawmill.Info($"Spawning {comp.Prototype} at {coords}");
            // Harmony Change Start - Moffstation Dead Drop Port
            // Spawn(comp.Prototype, coords);
            var ent = Spawn(comp.Prototype, coords);

            if (comp.RadioMessage is {} radioMessage)
            {
                var message = Loc.GetString(radioMessage.Message, ("location", FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(ent))));
                _radio.SendRadioMessage(ent, message, radioMessage.Channel, ent);
            }
            // Harmony Change End
        }
    }
}
