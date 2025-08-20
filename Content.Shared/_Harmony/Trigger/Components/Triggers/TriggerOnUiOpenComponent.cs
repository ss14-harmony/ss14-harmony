using Content.Shared.Trigger.Components.Triggers;
using Robust.Shared.GameStates;

namespace Content.Shared._Harmony.Trigger.Components.Triggers;

/// <summary>
/// Triggers when a Bound User Interface is opened
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TriggerOnUiOpenComponent : BaseTriggerOnXComponent;
