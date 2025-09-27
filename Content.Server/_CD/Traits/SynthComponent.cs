namespace Content.Server._CD.Traits;

/// <summary>
/// Set players' blood to coolant
/// </summary>
[RegisterComponent, Access(typeof(SynthSystem))]
public sealed partial class SynthComponent : Component { } // Misfit - Refactor notification to own component
