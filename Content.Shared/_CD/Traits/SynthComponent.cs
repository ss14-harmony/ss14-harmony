using Robust.Shared.GameStates; // Misfit - Move synthetic trait to shared

namespace Content.Shared._CD.Traits; // Misfit - Move synthetic trait to shared

/// <summary>
/// Set players' blood to coolant
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SynthSystem))] // Misfit - Move synthetic trait to shared
public sealed partial class SynthComponent : Component { } // Misfit - Refactor notification to own component
