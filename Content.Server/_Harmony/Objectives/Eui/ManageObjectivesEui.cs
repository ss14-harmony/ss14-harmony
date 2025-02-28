using System.Linq;
using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared.Mind;
using Content.Shared.Objectives;

namespace Content.Server.Objectives;

public sealed class ManageObjectivesEui : BaseEui
{
    private EntityManager _entityManager;
    private EntityUid _mind;
    public ManageObjectivesEui(EntityManager entityManager, EntityUid mind)
    {
        _entityManager = entityManager;
        _mind = mind;
    }

    public void refresh(){
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        List<ObjectiveContainerData> objList = new List<ObjectiveContainerData>();

        if (_entityManager.TryGetComponent<MindComponent>(_mind, out var mindComp))
            objList = mindComp.Objectives.Select(x => new ObjectiveContainerData(
                _entityManager.GetComponent<MetaDataComponent>(x).EntityName,
                _entityManager.GetComponent<MetaDataComponent>(x).EntityDescription
                )).ToList();

        return new ManageObjectivesEuiState(_entityManager.GetNetEntity(_mind), objList);
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
