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

public sealed partial class MalfOverloadMachineActionEvent : EntityTargetActionEvent
{
}

public sealed partial class MalfPurchaseSenseAIWireSnippedEvent : EntityEventArgs
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
public sealed partial class MalfOverloadMachineFinishedEvent : SimpleDoAfterEvent
{
}

public sealed partial class MalfHackApcActionEvent : EntityEventArgs
{
}

public sealed partial class MalfOverrideAiaActionEvent : EntityTargetActionEvent
{
}