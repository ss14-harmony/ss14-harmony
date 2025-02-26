using Content.Shared.Eui;
using Content.Shared.Objectives.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Objectives;

[Serializable, NetSerializable]
public sealed class ManageObjectivesEuiState : EuiStateBase
{
    public NetEntity Mind { get; }
    public List<ValueTuple<string, string>> Objectives { get; }
    public ManageObjectivesEuiState(NetEntity mind, List<ValueTuple<string, string>> objectives)
    {
        Mind = mind;
        Objectives = objectives;
    }
}
