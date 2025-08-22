using Content.Server._Harmony.Malfunction.Systems;
using Robust.Shared.Audio;

namespace Content.Server._Harmony.Malfunction.Components;

/// <summary>
/// Indicates that this AI has activated the Doomsday Device, which gibs all humanoid entities on the station after a countdown and guarantees a greentext for the Malf AI. Based on NukeComponent.
/// </summary>
[RegisterComponent, Access(typeof(DoomsdaySystem))]
public sealed partial class DoomsdayComponent : Component
{
    /// <summary>
    /// Default timer for the Doomsday Device in seconds.
    /// </summary>
    [DataField] public int Timer = 450;

    /// <summary>
    ///     Time until activation in seconds.
    /// </summary>
    [DataField] public float RemainingTime;

    /// <summary>
    ///     Check if we've already played the song so we don't do it again
    /// </summary>
    public bool PlayedDoomsdaySong = false;

    /// <remarks>
    ///     There will be music in the last part of the song; for now i'm just gonna use a nuke song but in future we could use Malf AI from the BeeStation soundtrack or LAW 2, OPEN ARMORY by mrjajkes if we get permission idk how this stuff works
    /// </remarks>
    [DataField] public SoundSpecifier ArmMusic = new SoundCollectionSpecifier("NukeMusic");
    [DataField] public SoundSpecifier DisarmSound = new SoundPathSpecifier("/Audio/Misc/notice2.ogg");

    [DataField] public string AlertLevelOnActivate = "delta";
    [DataField] public string AlertLevelOnDeactivate = "green";

    public EntityUid? InitialGrid;

}
