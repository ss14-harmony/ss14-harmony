using System.Text.RegularExpressions;
using Content.Server._Harmony.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server._Harmony.Speech.EntitySystems;

public sealed class IlleismAccentSystem : RelayAccentSystem<IlleismAccentComponent>
{
    // I am going to Sec -> NAME is going to Sec
    private static readonly Regex RegexIAmUpper = new(@"\bI\s*AM\b|\bI'?M\b");
    private static readonly Regex RegexIAmLower = new(@"\bi\s*am\b|\bI'?m\b", RegexOptions.IgnoreCase);

    // I have it -> NAME has it
    private static readonly Regex RegexIHaveUpper = new(@"\bI\s*HAVE\b|\bI'?VE\b");
    private static readonly Regex RegexIHaveLower = new(@"\bi\s*have\b|\bI'?ve\b", RegexOptions.IgnoreCase);

    // I do! -> NAME does!
    private static readonly Regex RegexIDoUpper = new(@"\bI\s*DO\b");
    private static readonly Regex RegexIDoLower = new(@"\bi\s*do\b", RegexOptions.IgnoreCase);

    // I don't! -> NAME doesn't!
    private static readonly Regex RegexIDontUpper = new(@"\bI\s+DON'?T\b");
    private static readonly Regex RegexIDontLower = new(@"\bi\s+don'?t\b", RegexOptions.IgnoreCase);

    // I/Myself -> NAME
    private static readonly Regex RegexMyselfUpper = new(@"\bMYSELF\b");
    private static readonly Regex RegexI = new(@"\bI\b|\bmyself\b", RegexOptions.IgnoreCase);

    // Me -> NAME
    private static readonly Regex RegexMeUpper = new(@"\bME\b");
    private static readonly Regex RegexMeLower = new(@"\bme\b", RegexOptions.IgnoreCase);

    // My crowbar -> NAME's crowbar
    // That's mine! -> That's NAME's
    private static readonly Regex RegexMyUpper = new(@"\bMY\b|\bMINE\b");
    private static readonly Regex RegexMyLower = new(@"\bmy\b|\bmine\b", RegexOptions.IgnoreCase);

    // I'll do it -> NAME'll do it
    private static readonly Regex RegexIllUpper = new(@"\bI'LL\b");
    private static readonly Regex RegexIllLower = new(@"\bi'll\b", RegexOptions.IgnoreCase);

    private bool MostlyUppercase(string message)
    {
        int totalLetters = 0;
        int uppercaseLetters = 0;

        // Iterate through each character in the string
        foreach (char c in message)
        {
            if (char.IsLetter(c)) // Check if the character is a letter
            {
                totalLetters++;
                if (char.IsUpper(c)) // Check if the letter is uppercase
                {
                    uppercaseLetters++;
                }
            }
        }
        if (totalLetters < 2)
        {
            return false;
        }
        return uppercaseLetters > totalLetters / 2;
    }

    public override string Accentuate(string message, Entity<IlleismAccentComponent>? entity = null)
    {
        var msg = message;

        if (entity == null)
            return message;

        var ent = entity.Value;

        var name = Name(ent).Split(' ')[0];
        if (name == Name(ent))
        {
            name = name.Split('-')[0];
        }
        var upperName = name.ToUpper();

        // I am going to Sec -> NAME is going to Sec
        msg = RegexIAmUpper.Replace(msg, upperName + " IS");
        msg = RegexIAmLower.Replace(msg, name + " is");

        // I have it -> NAME has it
        msg = RegexIHaveUpper.Replace(msg, upperName + " HAS");
        msg = RegexIHaveLower.Replace(msg, name + " has");

        // I do! -> NAME does!
        msg = RegexIDoUpper.Replace(msg, upperName + " DOES");
        msg = RegexIDoLower.Replace(msg, name + " does");

        // I don't! -> NAME doesn't!
        msg = RegexIDontUpper.Replace(msg, upperName + " DOESN'T");
        msg = RegexIDontLower.Replace(msg, name + " doesn't");

		// I'll do it -> NAME will do it
        msg = RegexIllUpper.Replace(msg, upperName + " WILL");
        msg = RegexIllLower.Replace(msg, name + " will");

        // I/myself -> NAME
        msg = RegexMyselfUpper.Replace(msg, upperName);
        if (MostlyUppercase(message))
        {
            msg = RegexI.Replace(msg, upperName);
        }
        else
        {
            msg = RegexI.Replace(msg, name);
        }

        // Me -> NAME
        msg = RegexMeUpper.Replace(msg, upperName);
        msg = RegexMeLower.Replace(msg, name);

        // My crowbar -> NAME's crowbar
        msg = RegexMyUpper.Replace(msg, upperName + "'S");
        msg = RegexMyLower.Replace(msg, name + "'s");

        return msg;
    }
};
