using System.Globalization;
using System.Text;

namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>A vowel nucleus of a Dutch word.</summary>
/// <param name="Start">Offset in the lower-case word.</param>
/// <param name="Length">Letters of the nucleus.</param>
/// <param name="Vowel">The nucleus with diacritics removed (e.g. <c>ee</c>, <c>ij</c>, <c>eau</c>).</param>
public sealed record DutchNucleus(int Start, int Length, string Vowel);

/// <summary>
/// Dutch spelling-based syllables and rhyme keys (research R-04): vowel groups with the digraph and trigraph table, a diaeresis starts a
/// new syllable, <c>ij</c> is one vowel, <c>y</c> before a vowel at the start is a consonant, plus an exception list for loanwords.
/// </summary>
public static class DutchPhonology
{
    private static readonly string[] Units = ["eeu", "ieu", "oei", "aai", "ooi", "eau", "aa", "ee", "oo", "uu", "ie", "oe", "eu", "ui", "ij", "ei", "au", "ou"];

    private static readonly Dictionary<string, int> Exceptions = new(StringComparer.Ordinal)
    {
        ["team"] = 1, ["cake"] = 1, ["love"] = 1, ["jeans"] = 1, ["steak"] = 1, ["beat"] = 1, ["deadline"] = 2, ["website"] = 2, ["okay"] = 2,
        ["museum"] = 3,
    };

    /// <summary>Syllables of a Dutch word.</summary>
    /// <param name="word">The word as written.</param>
    /// <returns>The count (0 for <c>'k</c>, <c>'t</c>).</returns>
    public static int Syllables(string word)
    {
        var key = Lower(word);
        if (Exceptions.TryGetValue(key, out var known))
        {
            return known;
        }

        var count = Nuclei(key).Count;
        return count == 0 && IsInnerContraction(key) ? 1 : count;
    }

    /// <summary>The vowel nuclei of a Dutch word.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The nuclei in order.</returns>
    public static IReadOnlyList<DutchNucleus> Nuclei(string word)
    {
        var key = Lower(word);
        var plain = Plain(key);
        var nuclei = new List<DutchNucleus>();
        var i = 0;
        while (i < key.Length)
        {
            if (!IsVowel(key, i))
            {
                i++;
                continue;
            }

            var length = UnitLength(key, plain, i);
            nuclei.Add(new DutchNucleus(i, length, plain.Substring(i, length)));
            i += length;
        }

        return MergeTion(key, nuclei);
    }

    /// <summary>The rhyme key: the stressed nucleus (long or short) and the normalised rest of the word.</summary>
    /// <param name="word">The word.</param>
    /// <returns>Vowel and tail; empty vowel when the word has none.</returns>
    public static (string Vowel, string Tail) RhymeKey(string word)
    {
        var key = Lower(word);
        var plain = Plain(key);
        var nuclei = Nuclei(key);
        if (nuclei.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        var stressed = nuclei[Stressed(plain, nuclei)];
        var tailStart = stressed.Start + stressed.Length;
        var vowel = Long(stressed, plain, tailStart);
        return (Normalize(vowel), NormalizeTail(plain[tailStart..]));
    }

    // Penultimate before a schwa ending (-e, -en, -er, -el), else the last; unstressed prefixes (be-, ge-, ver-, ont-) are never
    // last before a stressed syllable, so they need no extra rule.
    private static int Stressed(string plain, IReadOnlyList<DutchNucleus> nuclei)
    {
        var last = nuclei.Count - 1;
        var schwaEnding = last > 0 && nuclei[last].Vowel == "e"
            && (plain.EndsWith('e') || plain.EndsWith("en", StringComparison.Ordinal) || plain.EndsWith("er", StringComparison.Ordinal) || plain.EndsWith("el", StringComparison.Ordinal));
        return schwaEnding ? last - 1 : last;
    }

    private static string Long(DutchNucleus nucleus, string plain, int tailStart)
    {
        if (nucleus.Length != 1 || !"aeou".Contains(nucleus.Vowel[0], StringComparison.Ordinal))
        {
            return nucleus.Vowel;
        }

        var open = tailStart >= plain.Length || (tailStart + 1 < plain.Length && !IsPlainVowel(plain[tailStart]) && IsPlainVowel(plain[tailStart + 1]));
        return open ? new string(nucleus.Vowel[0], 2) : nucleus.Vowel;
    }

    private static string Normalize(string vowel) => vowel switch
    {
        "ei" => "ij",
        "au" => "ou",
        "y" => "i",
        _ => vowel,
    };

    private static string NormalizeTail(string tail)
    {
        var text = tail.Replace("ei", "ij", StringComparison.Ordinal).Replace("au", "ou", StringComparison.Ordinal);
        text = text.EndsWith("en", StringComparison.Ordinal) && text.Length > 2 ? text[..^1] : text;
        text = text.EndsWith('d') ? text[..^1] + "t" : text;
        return text.EndsWith('g') && !text.EndsWith("ng", StringComparison.Ordinal) ? text[..^1] + "ch" : text;
    }

    private static int UnitLength(string key, string plain, int i)
    {
        if (IsDiaeresis(key[i]))
        {
            return Prefix(plain, i, key) is { } unit ? unit.Length : 1;
        }

        return Prefix(plain, i, key)?.Length ?? 1;
    }

    // A unit may not continue into a letter with a diaeresis (it starts a new syllable).
    private static string? Prefix(string plain, int i, string key) =>
        Units.FirstOrDefault(unit => string.CompareOrdinal(plain, i, unit, 0, unit.Length) == 0 && !Enumerable.Range(i + 1, unit.Length - 1).Any(j => IsDiaeresis(key[j])));

    private static List<DutchNucleus> MergeTion(string key, List<DutchNucleus> nuclei)
    {
        if (nuclei.Count >= 2 && key.EndsWith("tion", StringComparison.Ordinal) && nuclei[^1].Vowel == "o" && nuclei[^2].Vowel == "i" && nuclei[^2].Start == nuclei[^1].Start - 1)
        {
            nuclei[^2] = nuclei[^2] with { Length = 2, Vowel = "io" };
            nuclei.RemoveAt(nuclei.Count - 1);
        }

        return nuclei;
    }

    private static bool IsVowel(string key, int i) => key[i] switch
    {
        'y' => i > 0 || key.Length == 1 || !IsPlainVowel(Plain(key)[1]),
        _ => IsPlainVowel(Plain(key[i].ToString())[0]),
    };

    private static bool IsPlainVowel(char c) => "aeiouy".Contains(c, StringComparison.Ordinal);

    private static bool IsDiaeresis(char c) => c is 'ë' or 'ï' or 'ö' or 'ü' or 'ä';

    private static bool IsInnerContraction(string key)
    {
        var apostrophe = key.IndexOf('\'', StringComparison.Ordinal);
        return apostrophe > 0 && apostrophe < key.Length - 1;
    }

    private static string Lower(string word) => (word ?? string.Empty).Trim().Replace('’', '\'').ToLower(CultureInfo.InvariantCulture).Normalize(NormalizationForm.FormC);

    private static string Plain(string key)
    {
        var builder = new StringBuilder(key.Length);
        foreach (var c in key)
        {
            builder.Append(c.ToString().Normalize(NormalizationForm.FormD)[0]);
        }

        return builder.ToString();
    }
}
