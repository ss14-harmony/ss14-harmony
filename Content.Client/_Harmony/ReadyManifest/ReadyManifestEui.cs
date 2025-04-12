using Content.Client._Harmony.ReadyManifest.UI;
using Content.Client.Eui;
using Content.Shared._Harmony.ReadyManifest;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Harmony.ReadyManifest;

[UsedImplicitly]
public sealed class ReadyManifestEui : BaseEui
{
    private readonly ReadyManifestUi _window;

    public ReadyManifestEui()
    {
        _window = new ReadyManifestUi();

        _window.OnClose += () =>
        {
            SendMessage(new CloseEuiMessage());
        };
    }

    public override void Opened()
    {
        base.Opened();

        _window.OpenCentered();
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);

        if (state is not ReadyManifestEuiState cast)
            return;

        _window.RebuildUI(cast.JobCounts);
    }

    public override void Closed()
    {
        base.Closed();

        _window.Close();
    }
}
