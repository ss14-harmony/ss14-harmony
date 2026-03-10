using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Harmony.ReadyManifest.UI;

[CommonSheetlet]
public sealed class ReadyManifestSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        return
        [
            E<Label>()
                .Class(ReadyManifestJobListing.StyleClassReadyIndicatorNoReady)
                .FontColor(Color.Red),

            E<Label>()
                .Class(ReadyManifestJobListing.StyleClassReadyIndicatorLowReady)
                .FontColor(Color.Red),

            E<Label>()
                .Class(ReadyManifestJobListing.StyleClassReadyIndicatorMediumReady)
                .FontColor(Color.Orange),

            E<Label>()
                .Class(ReadyManifestJobListing.StyleClassReadyIndicatorHighReady)
                .FontColor(Color.LightGreen),
        ];
    }
}
