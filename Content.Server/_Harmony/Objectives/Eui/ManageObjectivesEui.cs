using System.Linq;
using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared.Mind;
using Content.Shared.Objectives;

namespace Content.Server.Objectives;

public sealed class ManageObjectivesEui : BaseEui
{
    public readonly EntityManager _entityManager = default!;
    private EntityUid _mind;
    public ManageObjectivesEui(EntityUid mind)
    {
        _mind = mind;
    }

    public void refresh(){
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new ManageObjectivesEuiState(_entityManager.GetNetEntity(_mind));
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        switch (msg)
        {
            case CloseEuiMessage:
                Close();
                break;
        }

    }
}
