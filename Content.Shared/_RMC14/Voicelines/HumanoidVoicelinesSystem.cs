using Content.Shared._RMC14.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Voicelines;

public sealed class HumanoidVoicelinesSystem : EntitySystem
{
    [Dependency] private readonly INetConfigurationManager _config = default!;

    private static readonly ProtoId<SpeciesPrototype> ArachnidSpecies = "Arachnid";
    private static readonly ProtoId<SpeciesPrototype> DionaSpecies = "Diona";
    private static readonly ProtoId<SpeciesPrototype> DwarfSpecies = "Dwarf";
    // private static readonly ProtoId<SpeciesPrototype> FelinidSpecies = "Felinid"; // Harmony - No Felenids
    private static readonly ProtoId<SpeciesPrototype> HumanSpecies = "Human";
    private static readonly ProtoId<SpeciesPrototype> MothSpecies = "Moth";
    private static readonly ProtoId<SpeciesPrototype> ReptilianSpecies = "Reptilian";
    private static readonly ProtoId<SpeciesPrototype> SlimeSpecies = "SlimePerson";
    private static readonly ProtoId<SpeciesPrototype> AvaliSpecies = "Avali";
    // private static readonly ProtoId<SpeciesPrototype> VulpkaninSpecies = "Vulpkanin"; // Harmony - No Vulps
    // private static readonly ProtoId<SpeciesPrototype> RodentiaSpecies = "Rodentia"; // Harmony - No Rodentia
    // private static readonly ProtoId<SpeciesPrototype> FeroxiSpecies = "Feroxi"; // Harmony - No Feroxi
    // private static readonly ProtoId<SpeciesPrototype> SkrellSpecies = "Skrell"; // Harmony - No Skrell

    private readonly Dictionary<ProtoId<SpeciesPrototype>, CVarDef> _voicelineCVars = new()
    {
        [ArachnidSpecies] = RMCCVars.RMCPlayVoicelinesArachnid,
        [DionaSpecies] = RMCCVars.RMCPlayVoicelinesDiona,
        [DwarfSpecies] = RMCCVars.RMCPlayVoicelinesDwarf,
        // [FelinidSpecies] = RMCCVars.RMCPlayVoicelinesFelinid, // Harmony - No Felenids
        [HumanSpecies] = RMCCVars.RMCPlayVoicelinesHuman,
        [MothSpecies] = RMCCVars.RMCPlayVoicelinesMoth,
        [ReptilianSpecies] = RMCCVars.RMCPlayVoicelinesReptilian,
        [SlimeSpecies] = RMCCVars.RMCPlayVoicelinesSlime,
        [AvaliSpecies] = RMCCVars.RMCPlayVoicelinesAvali,
        // [VulpkaninSpecies] = RMCCVars.RMCPlayVoicelinesVulpkanin, // Harmony - No Vulps
        // [RodentiaSpecies] = RMCCVars.RMCPlayVoicelinesRodentia, // Harmony - No Rodentia
        // [FeroxiSpecies] = RMCCVars.RMCPlayVoicelinesFeroxi, // Harmony - No Feroxi
        // [SkrellSpecies] = RMCCVars.RMCPlayVoicelinesSkrell, // Harmony - No Skrell
    };

    private readonly Dictionary<ProtoId<SpeciesPrototype>, CVarDef> _emoteCVars = new()
    {
        [ArachnidSpecies] = RMCCVars.RMCPlayEmotesArachnid,
        [DionaSpecies] = RMCCVars.RMCPlayEmotesDiona,
        [DwarfSpecies] = RMCCVars.RMCPlayEmotesDwarf,
        // [FelinidSpecies] = RMCCVars.RMCPlayEmotesFelinid, // Harmony - No Felenids
        [HumanSpecies] = RMCCVars.RMCPlayEmotesHuman,
        [MothSpecies] = RMCCVars.RMCPlayEmotesMoth,
        [ReptilianSpecies] = RMCCVars.RMCPlayEmotesReptilian,
        [SlimeSpecies] = RMCCVars.RMCPlayEmotesSlime,
        [AvaliSpecies] = RMCCVars.RMCPlayEmotesAvali,
        // [VulpkaninSpecies] = RMCCVars.RMCPlayEmotesVulpkanin, // Harmony - No Vulps
        // [RodentiaSpecies] = RMCCVars.RMCPlayEmotesRodentia, // Harmony - No Rodentia
        // [FeroxiSpecies] = RMCCVars.RMCPlayEmotesFeroxi, // Harmony - No Feroxi
        // [SkrellSpecies] = RMCCVars.RMCPlayEmotesSkrell, // Harmony - No Skrell
    };

    private EntityQuery<HumanoidAppearanceComponent> _humanoidAppearanceQuery;

    public override void Initialize()
    {
        _humanoidAppearanceQuery = GetEntityQuery<HumanoidAppearanceComponent>();
    }

    public bool ShouldPlayVoiceline(Entity<HumanoidAppearanceComponent?> vocalizer, ICommonSession forPlayer)
    {
        if (forPlayer.AttachedEntity == vocalizer &&
            !_config.GetClientCVar(forPlayer.Channel, RMCCVars.RMCPlayVoicelinesYourself))
        {
            return false;
        }

        if (!_humanoidAppearanceQuery.Resolve(vocalizer, ref vocalizer.Comp, false) ||
            !_voicelineCVars.TryGetValue(vocalizer.Comp.Species, out var play))
        {
            return true;
        }

        return _config.GetClientCVar<bool>(forPlayer.Channel, play.Name);
    }

    public bool ShouldPlayEmote(Entity<HumanoidAppearanceComponent?> vocalizer, ICommonSession forPlayer)
    {
        if (forPlayer.AttachedEntity == vocalizer &&
            !_config.GetClientCVar(forPlayer.Channel, RMCCVars.RMCPlayEmotesYourself))
        {
            return false;
        }

        if (!_humanoidAppearanceQuery.Resolve(vocalizer, ref vocalizer.Comp, false) ||
            !_emoteCVars.TryGetValue(vocalizer.Comp.Species, out var play))
        {
            return true;
        }

        return _config.GetClientCVar<bool>(forPlayer.Channel, play.Name);
    }

// Harmony Change Start
    public bool ShouldPlayVoicelines(Entity<HumanoidAppearanceComponent?> vocalizer, ICommonSession forPlayer)
    {
        if (forPlayer.AttachedEntity == vocalizer &&
            !_config.GetClientCVar(forPlayer.Channel, RMCCVars.RMCPlayVoicelinesYourself))
        {
            return false;
        }

        if (!_humanoidAppearanceQuery.Resolve(vocalizer, ref vocalizer.Comp, false) ||
            !_voicelineCVars.TryGetValue(vocalizer.Comp.Species, out var play))
        {
            return true;
        }
// Harmony Change End

        return _config.GetClientCVar<bool>(forPlayer.Channel, play.Name);
    }
}
