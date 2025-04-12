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
        return new ReadyManifestEuiState(_readyManifestSystem.GetReadyManifest());
    }

    public override void Closed()
    {
        base.Closed();

        _readyManifestSystem.CloseEui(Player);
    }
}
