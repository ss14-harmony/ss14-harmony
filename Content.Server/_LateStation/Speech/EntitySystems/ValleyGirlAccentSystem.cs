using System.Text.RegularExpressions;
using System.Text;
using Content.Server.Speech;
using Content.Server.Speech.EntitySystems;
using Content.Server._Latestation.Speech.Components;
using Robust.Shared.Random;
using System.Linq;
using System.Diagnostics;

namespace Content.Server._Latestation.Speech.EntitySystems;

public sealed class ValleyGirlAccentSystem : EntitySystem
{
    //Words ending in -ing = in'. Bein', Darlin', etc.
    //Taken from mobster accent.
    private static readonly Regex RegexIng = new(@"(?<=\w\w)(in)g(?!\w)", RegexOptions.IgnoreCase);
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ValleyGirlAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(Entity<ValleyGirlAccentComponent> ent, ref AccentGetEvent args)
    {
        var message = args.Message;

        //The main word replacement is done through replacement accent system
        message = _replacement.ApplyReplacements(message, "valleygirl");

        //the G in -ing words. Thinkin', darlin', etc.
        message = RegexIng.Replace(message, "$1'");

        //Mid-sentence ", like, " for maximum suffering
        if (_random.Prob(0.6f))
        {
            var words = message.Split(' ').ToList();
            if (words.Count() > 1)
            {
                var placement = _random.Next(1, words.Count());
                words[placement - 1] += ',';
                words.Insert(placement, "like,");
                string updatedMessage = string.Join(" ", words);
                message = updatedMessage;
            }
        }

        // Like, Prefixes
        if (_random.Prob(0.25f))
        {
            var pick = _random.Next(1, 3);
            var prefix = Loc.GetString($"accent-valleygirl-prefix-{pick}");

            //Lowercases the first word since the prefix takes priority.
            //I don't care if the first word is "I " leave me alone
            message = message[0].ToString().ToLower() + message.Remove(0, 1);
            message = prefix + " " + message;
        }

        // Suffixes, like totes.
        if (_random.Prob(0.4f))
        {
            var pick = _random.Next(1, 5);
            var suffix = Loc.GetString($"accent-valleygirl-suffix-{pick}");
            message += suffix;
        }


        args.Message = message;
    }
}