using Content.Shared._Harmony.ReadyManifest;

namespace Content.Client._Harmony.ReadyManifest;

public sealed class ReadyManifestSystem : SharedReadyManifestSystem
{
    public void RequestReadyManifest()
    {
        RaiseNetworkEvent(new RequestReadyManifestMessage());
    }
}
