using Content.Shared.Actions;
using Content.Shared.DoAfter;

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

public sealed partial class MalfOverloadMachineFinishedEvent : SimpleDoAfterEvent
{
}
public sealed partial class MalfHackApcActionEvent : EntityTargetActionEvent
{
}

public sealed partial class MalfOverrideAiaActionEvent : EntityTargetActionEvent
{
}