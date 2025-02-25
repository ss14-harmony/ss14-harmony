using Content.Client.Eui;
using Content.Client._Harmony.Objectives;
using Content.Shared.Eui;
using Content.Shared.Objectives;

namespace Content.Server.Objectives;

public sealed class ManageObjectivesEui : BaseEui
{
    public readonly EntityManager _entityManager = default!;
    private ManageObjectivesUi _manageObjectivesUi;
    public ManageObjectivesEui()
    {
        _manageObjectivesUi = new ManageObjectivesUi();
        _manageObjectivesUi.OnClose += () => CloseWindow();
        _manageObjectivesUi.SaveButton.OnPressed += _ => OnSubmitButtonPressed();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not ManageObjectivesEuiState s)
            return;

        _manageObjectivesUi.UpdateState(_entityManager.GetEntity(s.Mind));
    }

    private void OnSubmitButtonPressed()
    {
        //_manageObjectivesUi.UpdateState();
        CloseWindow();
    }

    private void CloseWindow()
    {
        SendMessage(new CloseEuiMessage());
        _manageObjectivesUi.Close();
    }

    public override void Opened()
    {
        _manageObjectivesUi.OpenCentered();
    }
}
