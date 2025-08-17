using Content.Shared.Actions;

namespace Content.Shared._Harmony.Malfunction;

public sealed partial class MalfShopActionEvent : InstantActionEvent
{
}

public sealed partial class MalfOverloadMachineActionEvent : EntityTargetActionEvent
{
}

public sealed partial class MalfHackApcActionEvent : EntityTargetActionEvent
{
}