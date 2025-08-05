using Content.Server._Harmony.BloodOath.EntitySystems;

namespace Content.Server._Harmony.Objectives.Components;

/// <summary>
/// Marker component to show that an objective should be removed when the blood brother is deconverted.
/// </summary>
[RegisterComponent, Access(typeof(BloodBoundSystem))]
public sealed partial class ConvertedBloodBoundObjectiveComponent : Component;
