using Content.Client.UserInterface.Controls;
using Content.Shared.Cybernetics.UI;
using Robust.Client.UserInterface;

namespace Content.Client.Cybernetics.UI;

public sealed class CyberArmSelectMenuBoundUserInterface : BoundUserInterface
{
    private SimpleRadialMenu? _menu;

    public CyberArmSelectMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not CyberArmSelectBoundUserInterfaceState cState || _menu == null)
            return;

        var models = ConvertToButtons(cState.Items);
        _menu.SetButtons(models);
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> ConvertToButtons(List<CyberArmSelectItemEntry> items)
    {
        foreach (var entry in items)
        {
            if (!EntMan.TryGetEntity(entry.Entity, out var entity))
                continue;

            var option = new RadialMenuActionOption<NetEntity>(HandleMenuOptionClick, entry.Entity)
            {
                ToolTip = entry.Name,
                IconSpecifier = RadialMenuIconSpecifier.With(entity)
            };
            yield return option;
        }
    }

    private void HandleMenuOptionClick(NetEntity entity)
    {
        SendPredictedMessage(new CyberArmSelectRequestMessage(entity));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _menu = null;
    }
}
