using System.Text;
using Robust.Shared.Random;
using Content.Server.Speech.EntitySystems;
using Content.Server._Harmony.Speech.Components;
using Content.Server.Speech.Prototypes;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Server._Harmony.Speech.EntitySystems;

public sealed class ScandinavianAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;
    
    private static readonly ProtoId<ReplacementAccentPrototype> AccentName = new("scandinavian");

    private static readonly IReadOnlyDictionary<char, char[]> Vowels = new Dictionary<char, char[]>()
    {
        { 'A',  ['Å','Ä','Æ'] },
        { 'a',  ['å','ä','æ'] },
        { 'O',  ['Ö','Ø'] },
        { 'o',  ['ö','ø'] },
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<ScandinavianAccentComponent, AccentGetEvent>(OnAccent);
    }

    public string Accentuate(string message)
    {
        var msg = message;

        // Apply word replacements
        msg = _replacement.ApplyReplacements(msg, AccentName);

        // Random Umlaut Time! Happily taken from the German code.
        var msgBuilder = new StringBuilder(msg);
        var umlautCooldown = 0;
        for (var i = 0; i < msgBuilder.Length; i++)
        {
            var tempchar = msgBuilder[i];

            msgBuilder[i] = tempchar switch
            {
                'W' => 'V',
                'w' => 'v',
                'J' => 'Y',
                'j' => 'y',
                _ => msgBuilder[i],
            };

            if (umlautCooldown == 0)
            {
                if (_random.Prob(0.4f)) // 40% of all eligible vowels become umlauts
                {
                    msgBuilder[i] = tempchar switch
                    {
                        'A' => _random.Pick(Vowels['A']),
                        'a' => _random.Pick(Vowels['a']),
                        'O' => _random.Pick(Vowels['O']),
                        'o' => _random.Pick(Vowels['o']),
                        _ => msgBuilder[i]
                    };
                    umlautCooldown = 4;
                }
            }
            else
            {
                umlautCooldown--;
            }
        }

        return msgBuilder.ToString();
    }

    private void OnAccent(Entity<ScandinavianAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
