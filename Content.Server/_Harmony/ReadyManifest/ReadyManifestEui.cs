using Content.Server.EUI;
using Content.Shared._Harmony.ReadyManifest;

namespace Content.Server._Harmony.ReadyManifest;

public sealed class ReadyManifestEui : BaseEui
{
    private readonly ReadyManifestSystem _readyManifestSystem;

    public ReadyManifestEui(ReadyManifestSystem readyManifestSystem)
    {
        _readyManifestSystem = readyManifestSystem;
    }

    public override ReadyManifestEuiState GetNewState()
    {
        var entries = _readyManifestSystem.GetReadyManifest();
        return new ReadyManifestEuiState(entries);
    }

    public override void Closed()
    {
        base.Closed();

        _readyManifestSystem.CloseEui(Player);
    }
}
