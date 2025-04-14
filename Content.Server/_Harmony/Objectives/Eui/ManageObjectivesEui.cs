using System.Linq;
using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared.Mind;
using Content.Shared.Objectives;

namespace Content.Server.Objectives;

public sealed class ManageObjectivesEui : BaseEui
{
    private EntityManager _entityManager;
    private SharedMindSystem _mindSystem;
    private EntityUid _mind;
    public ManageObjectivesEui(EntityManager entityManager, SharedMindSystem mindSystem, EntityUid mind)
    {
        _entityManager = entityManager;
        _mindSystem = mindSystem;
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
            case AddObjectiveMessage:
                if (_entityManager.TryGetComponent<MindComponent>(_mind, out var mindComp))
                    _mindSystem.TryAddObjective(_mind, mindComp, "CustomPlayerObjective");
                break;
            case CloseEuiMessage:
                Close();
                break;
        }

    }
}
