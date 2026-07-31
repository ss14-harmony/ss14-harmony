//Based on RMC14 code found at https://github.com/RMC-14/RMC-14/blob/f365e04dd8d4053149ae324af773633ef287253f/Content.Shared/_RMC14/Voicelines/HumanoidVoicelinesSystem.cs
//Renamespaced to _Box after a thorough rewrite.
using Content.Shared._RMC14.CCVar;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Interaction.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Box.Audio;

/// <summary>
/// System for muting speech, emote, and interaction sounds based on user options.
/// </summary>
public sealed class SpeciesMutingSystem : EntitySystem
{
    [Dependency] private readonly INetConfigurationManager _config = default!;

    private static readonly ProtoId<SpeechSoundsPrototype> ArachnidVoice = "Arachnid";
    //Dionae use human speech sounds.
    private static readonly ProtoId<SpeechSoundsPrototype> DwarfVoice = "Bass";
    private static readonly ProtoId<SpeechSoundsPrototype> HumanVoice = "Alto";
    private static readonly ProtoId<SpeechSoundsPrototype> MothVoice = "Moth";
    private static readonly ProtoId<SpeechSoundsPrototype> ReptilianVoice = "Lizard";
    private static readonly ProtoId<SpeechSoundsPrototype> SlimeVoice = "Slime";
    private static readonly ProtoId<SpeechSoundsPrototype> AvaliVoice = "MaleAvali";
    private static readonly ProtoId<SpeechSoundsPrototype> VulpkaninVoice = "Vulpkanin";
    private static readonly ProtoId<SpeechSoundsPrototype> RodentiaVoice = "Squeak";
    private static readonly ProtoId<SpeechSoundsPrototype> VoxVoice = "Vox";
    private static readonly ProtoId<SpeechSoundsPrototype> ScurretVoice = "Wawa";
    private static readonly ProtoId<SpeechSoundsPrototype> IPCVoice = "Pai";
    //Thaven use human speech sounds.
    private static readonly ProtoId<SpeechSoundsPrototype> AllulaloVoice = "Allulalo";

    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> ArachnidEmotes = ["UnisexArachnid"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> DionaEmotes = ["UnisexDiona"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> DwarfEmotes = ["UnisexDwarf", "FemaleDwarf"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> HumanEmotes = ["FemaleHuman", "MaleHuman"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> MothEmotes = ["UnisexMoth"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> ReptilianEmotes = ["FemaleReptilian", "MaleReptilian"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> SlimeEmotes = ["FemaleSlime", "MaleSlime"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> AvaliEmotes = ["FemaleAvali", "MaleAvali"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> VulpkaninEmotes = ["FemaleVulpkanin", "MaleVulpkanin"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> RodentiaEmotes = ["FemaleRodentia", "MaleRodentia"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> VoxEmotes = ["UnisexVox"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> ScurretEmotes = ["Scurret"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> IPCEmotes = ["UnisexIPC"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> ThavenEmotes = ["MaleThaven"];
    private static readonly HashSet<ProtoId<EmoteSoundsPrototype>> AllulaloEmotes = ["Allulalo"];

    /// <summary>
    /// A collection of speech sounds to mute, and their associated CVar.
    /// </summary>
    private readonly Dictionary<ProtoId<SpeechSoundsPrototype>, CVarDef> _voicelineCVars = new()
    {
        [ArachnidVoice] = RMCCVars.RMCPlayVoicelinesArachnid,
        [DwarfVoice] = RMCCVars.RMCPlayVoicelinesDwarf,
        [HumanVoice] = RMCCVars.RMCPlayVoicelinesHuman,
        [MothVoice] = RMCCVars.RMCPlayVoicelinesMoth,
        [ReptilianVoice] = RMCCVars.RMCPlayVoicelinesReptilian,
        [SlimeVoice] = RMCCVars.RMCPlayVoicelinesSlime,
        [AvaliVoice] = RMCCVars.RMCPlayVoicelinesAvali,
        [VulpkaninVoice] = RMCCVars.RMCPlayVoicelinesVulpkanin,
        [RodentiaVoice] = RMCCVars.RMCPlayVoicelinesRodentia,
        [VoxVoice] = RMCCVars.RMCPlayVoicelinesVox,
        [ScurretVoice] = RMCCVars.RMCPlayVoicelinesScurret,
        [IPCVoice] = RMCCVars.RMCPlayVoicelinesIPC,
        [AllulaloVoice] = RMCCVars.RMCPlayVoicelinesAllulalo,
    };

    /// <summary>
    /// A collection of emote sound prototypes to mute, and their associated CVar.
    /// </summary>
    private readonly Dictionary<HashSet<ProtoId<EmoteSoundsPrototype>>, CVarDef> _emoteCVars = new()
    {
        [ArachnidEmotes] = RMCCVars.RMCPlayEmotesArachnid,
        [DionaEmotes] = RMCCVars.RMCPlayEmotesDiona,
        [DwarfEmotes] = RMCCVars.RMCPlayEmotesDwarf,
        [HumanEmotes] = RMCCVars.RMCPlayEmotesHuman,
        [MothEmotes] = RMCCVars.RMCPlayEmotesMoth,
        [ReptilianEmotes] = RMCCVars.RMCPlayEmotesReptilian,
        [SlimeEmotes] = RMCCVars.RMCPlayEmotesSlime,
        [AvaliEmotes] = RMCCVars.RMCPlayEmotesAvali,
        [VulpkaninEmotes] = RMCCVars.RMCPlayEmotesVulpkanin,
        [RodentiaEmotes] = RMCCVars.RMCPlayEmotesRodentia,
        [VoxEmotes] = RMCCVars.RMCPlayEmotesVox,
        [ScurretEmotes] = RMCCVars.RMCPlayEmotesScurret,
        [IPCEmotes] = RMCCVars.RMCPlayEmotesIPC,
        [ThavenEmotes] = RMCCVars.RMCPlayEmotesThaven,
        [AllulaloEmotes] = RMCCVars.RMCPlayEmotesAllulalo,
    };

    /// <summary>
    /// A collection of interact sounds (specified as sound paths) to mute, and their associated CVar.
    /// Copy the path used in the offending entity's yml to ensure accuracy.
    /// </summary>
    private readonly Dictionary<HashSet<String>, CVarDef> _interactPathCVars = new()
    {
        [["/Audio/Animals/wawa_chatter.ogg", "/Audio/Animals/wawa_chillin.ogg"]] = RMCCVars.RMCPlayEmotesScurret
    };

    /// <summary>
    /// A collection of interact sounds (specified as sound collections) to mute, and their associated CVar.
    /// </summary>
    private readonly Dictionary<HashSet<ProtoId<SoundCollectionPrototype>>, CVarDef> _interactCollectionCVars = new()
    {
        // Nothing here for now.
        // Example line will cause the desk bell to have its interact sounds muted if human emotes are muted.
        // In memory of the first round with the new system where we found out I broke the desk bell due to casting issues.
        //[["DeskBell"]] = RMCCVars.RMCPlayEmotesHuman
    };

    private EntityQuery<VocalComponent> _vocalQuery;
    private EntityQuery<SpeechComponent> _speechQuery;
    private EntityQuery<InteractionPopupComponent> _interactionQuery;

    public override void Initialize()
    {
        _vocalQuery = GetEntityQuery<VocalComponent>();
        _speechQuery = GetEntityQuery<SpeechComponent>();
        _interactionQuery = GetEntityQuery<InteractionPopupComponent>();
    }

    public bool ShouldPlayEmote(Entity<VocalComponent?> vocalizer, ICommonSession forPlayer)
    {
        if (forPlayer.AttachedEntity == vocalizer &&
            !_config.GetClientCVar(forPlayer.Channel, RMCCVars.RMCPlayEmotesYourself))
            return false;

        if (!_vocalQuery.Resolve(vocalizer, ref vocalizer.Comp, false))
            return true;

        if (vocalizer.Comp.EmoteSounds == null)
            return true;

        ProtoId<EmoteSoundsPrototype> sound = (ProtoId<EmoteSoundsPrototype>)vocalizer.Comp.EmoteSounds;
        CVarDef? play = null;
        foreach (var emote in _emoteCVars)
        {
            if (emote.Key.Contains(sound))
            {
                play = emote.Value;
                break;
            }
        }

        if (play == null)
            return true;

        return _config.GetClientCVar<bool>(forPlayer.Channel, play.Name);
    }

    public bool ShouldPlayVoicelines(Entity<SpeechComponent?> vocalizer, ICommonSession forPlayer)
    {
        if (forPlayer.AttachedEntity == vocalizer &&
            !_config.GetClientCVar(forPlayer.Channel, RMCCVars.RMCPlayVoicelinesYourself))
            return false;

        if (!_speechQuery.Resolve(vocalizer, ref vocalizer.Comp, false) ||
            !_voicelineCVars.TryGetValue(vocalizer.Comp.SpeechSounds ?? HumanVoice, out var play))
            return true;

        return _config.GetClientCVar<bool>(forPlayer.Channel, play.Name);
    }

    public bool ShouldPlayInteractionPopup(Entity<InteractionPopupComponent?> vocalizer, ICommonSession forPlayer)
    {
        if (forPlayer.AttachedEntity == vocalizer &&
            !_config.GetClientCVar(forPlayer.Channel, RMCCVars.RMCPlayEmotesYourself))
            return false;

        if (!_interactionQuery.Resolve(vocalizer, ref vocalizer.Comp, false))
            return true;

        HashSet<string> paths = [];
        HashSet<ProtoId<SoundCollectionPrototype>> collections = [];
        CVarDef? play = null;

        //Evil type conversion section because interact sounds aren't standardized as collections
        if (vocalizer.Comp.InteractSuccessSound != null)
        {
            if (vocalizer.Comp.InteractSuccessSound is SoundCollectionSpecifier specifier &&
                specifier.Collection != null)
            {
                collections.Add(specifier.Collection);
            }
            else
            {
                var x = (SoundPathSpecifier)vocalizer.Comp.InteractSuccessSound;
                paths.Add(x.Path.CanonPath);
            }
        }
        if (vocalizer.Comp.InteractFailureSound != null)
        {
            if (vocalizer.Comp.InteractFailureSound is SoundCollectionSpecifier specifier &&
                specifier.Collection != null)
            {
                collections.Add(specifier.Collection);
            }
            else
            {
                var x = (SoundPathSpecifier)vocalizer.Comp.InteractFailureSound;
                paths.Add(x.Path.CanonPath);
            }
        }

        if (collections.Count > 0)
        {
            foreach (var interact in _interactCollectionCVars)
            {
                if (interact.Key.Overlaps(collections))
                    play = interact.Value;
            }
        }

        if (paths.Count > 0)
        {
            foreach (var interact in _interactPathCVars)
            {
                if (interact.Key.Overlaps(paths))
                    play = interact.Value;
            }
        }

        if (play == null)
            return true;

        return _config.GetClientCVar<bool>(forPlayer.Channel, play.Name);
    }
}
