using Content.Shared.Eui;
using Content.Shared.Objectives.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Objectives;

[Serializable, NetSerializable]
public sealed class AddObjectiveMessage : EuiMessageBase
{

}

[Serializable, NetSerializable]
public sealed class ManageObjectivesEuiState : EuiStateBase
{
    public NetEntity Mind { get; }
    public List<ObjectiveContainerData> Objectives { get; }
    public ManageObjectivesEuiState(NetEntity mind, List<ObjectiveContainerData> objectives)
    {
        Mind = mind;
        Objectives = objectives;
    }
}

[Serializable, NetSerializable]
public sealed class ObjectiveContainerData : EuiStateBase
{
    public string Name { get; }
    public string Description { get; }
    public ObjectiveContainerData(string name, string desc)
    {
        Name = name;
        Description = desc;
    }
}
