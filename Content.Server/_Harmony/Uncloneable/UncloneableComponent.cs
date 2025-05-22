using Robust.Shared.GameStates;

namespace Content.Server._Harmony.Uncloneable;

/// <summary>
/// This is used for the uncloneable trait.
/// </summary>
[RegisterComponent, Access(typeof(UncloneableSystem))]
public sealed partial class UncloneableComponent : Component;
