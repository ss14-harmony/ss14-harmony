using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Harmony.Malfunction;

public sealed partial class MalfShopActionEvent : InstantActionEvent
{
}

public sealed partial class MalfDoomsdayStartEvent : InstantActionEvent
{
}

public sealed partial class MalfDoomsdayActivatedEvent : EntityEventArgs
{
}

public sealed partial class MalfLockdownEvent : InstantActionEvent
{
}

public sealed partial class MalfOverloadMachineActionEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed partial class MalfPurchaseOverrideAiaEvent : EntityEventArgs
{
}
[Serializable, NetSerializable]
public sealed partial class MalfPurchaseOverloadMachineEvent : EntityEventArgs
{
}
[Serializable, NetSerializable]
public sealed partial class MalfPurchaseVoiceModulationEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed partial class MalfPurchaseDisableControlPanelEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed partial class MalfPurchaseOverloadLightEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed partial class MalfPurchaseJamFirelockEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed partial class MalfPurchaseOverrideSafetyEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed partial class MalfPurchaseTurretUpgradeEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed partial class MalfOverloadMachineFinishedEvent : SimpleDoAfterEvent
{
}