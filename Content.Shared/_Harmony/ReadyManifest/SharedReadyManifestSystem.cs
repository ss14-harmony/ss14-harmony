using Robust.Shared.Serialization;

namespace Content.Shared._Harmony.ReadyManifest;

public abstract class SharedReadyManifestSystem : EntitySystem;

/// <summary>
/// Keeps the data related to a single job for the ready manifest.
/// </summary>
[Serializable, NetSerializable]
public struct ReadyManifestJobData
{
    /// <summary>
    /// The amount of people ready for this job on the low setting.
    /// </summary>
    public int LowReadies;

    /// <summary>
    /// The amount of people ready for this job on the medium setting.
    /// </summary>
    public int MediumReadies;

    /// <summary>
    /// The amount of people ready for this job on the high setting.
    /// </summary>
    public int HighReadies;
}

/// <summary>
/// Sent from the client when it wants to open a ready manifest eui.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestReadyManifestMessage : EntityEventArgs;
