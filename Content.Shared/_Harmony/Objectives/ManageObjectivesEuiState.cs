using Content.Shared.Eui;
using Content.Shared.Objectives.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Objectives;

[Serializable, NetSerializable]
public sealed class ManageObjectivesEuiState : EuiStateBase
{
    public NetEntity Mind { get; }
    public ManageObjectivesEuiState(NetEntity mind)
    {
        Mind = mind;
    }
}
