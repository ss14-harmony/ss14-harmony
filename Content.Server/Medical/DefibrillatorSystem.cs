using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Shared.Medical;
using Content.Shared.Mind;
using Robust.Shared.Player;

namespace Content.Server.Medical;

public sealed class DefibrillatorSystem : SharedDefibrillatorSystem
{
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!; // Funky - Cybermed

    protected override void OpenReturnToBodyEui(Entity<MindComponent> mind, ICommonSession session)
    {
        _eui.OpenEui(new ReturnToBodyEui(mind.Comp, _mindSystem, _player), session); // Funky - Cybermed
    }
}
