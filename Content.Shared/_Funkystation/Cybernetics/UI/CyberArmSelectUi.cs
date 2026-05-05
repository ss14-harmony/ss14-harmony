using Robust.Shared.Serialization;

namespace Content.Shared.Cybernetics.UI;

[Serializable, NetSerializable]
public sealed class CyberArmSelectBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<CyberArmSelectItemEntry> Items;

    public CyberArmSelectBoundUserInterfaceState(List<CyberArmSelectItemEntry> items)
    {
        Items = items;
    }
}

[Serializable, NetSerializable]
public sealed record CyberArmSelectItemEntry(NetEntity Entity, string Name);

[Serializable, NetSerializable]
public enum CyberArmSelectUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class CyberArmSelectRequestMessage(NetEntity SelectedItem) : BoundUserInterfaceMessage
{
    public NetEntity SelectedItem { get; } = SelectedItem;
}
