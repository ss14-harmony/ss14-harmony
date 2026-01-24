using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Harmony.Malfunction;

public sealed partial class MalfShopActionEvent : InstantActionEvent;

public sealed partial class MalfDoomsdayStartEvent : InstantActionEvent;

public sealed partial class MalfDestroyRcdsEvent : InstantActionEvent;

public sealed partial class MalfTransmitLawZeroEvent : InstantActionEvent;

public sealed partial class MalfDoomsdayActivatedEvent : EntityEventArgs;

public sealed partial class MalfLockdownEvent : InstantActionEvent;

public sealed partial class MalfOverloadMachineActionEvent : EntityEventArgs;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class MalfPurchaseOverrideAiaEvent : EntityEventArgs;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class MalfPurchaseOverloadMachineEvent : EntityEventArgs;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class MalfPurchaseVoiceModulationEvent : EntityEventArgs;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class MalfPurchaseDisableControlPanelEvent : EntityEventArgs;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class MalfPurchaseOverloadLightEvent : EntityEventArgs;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class MalfPurchaseJamFirelockEvent : EntityEventArgs;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class MalfPurchaseOverrideSafetyEvent : EntityEventArgs;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class MalfPurchaseTurretUpgradeEvent : EntityEventArgs;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class MalfPurchaseInternalMicroreactorEvent : EntityEventArgs;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class MalfOverloadMachineFinishedEvent : EntityEventArgs;
