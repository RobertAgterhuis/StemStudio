using System.Globalization;

namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>
/// English syllables and rhyme keys (research R-04): CMUdict vowel phones for known words (also <c>-in'</c> as <c>-ing</c> and
/// compounds of two known words), a spelling rule for the rest (vowel groups, silent final <c>e</c>, <c>-ed</c>).
/// </summary>
/// <param name="lexicon">Pronunciation lexicon.</param>
public sealed class EnglishPhonology(IPronunciationLexicon lexicon)
{
    private const int MinCompoundPart = 3;

    /// <summary>Syllables of an English word.</summary>
    /// <param name="word">The word as written.</param>
    /// <returns>The count.</returns>
    public int Syllables(string word)
    {
        var key = Key(word);
        if (key.Length == 0)
        {
            return 0;
        }

        return Phones(key) is { } phones ? phones.Count(IsVowelPhone) : Compound(key) ?? Rule(key);
    }

    /// <summary>The rhyme key: the last primary-stressed vowel (else secondary, else the last vowel) and the phones after it (spelling fallback for unknown words).</summary>
    /// <param name="word">The word.</param>
    /// <returns>Vowel and tail.</returns>
    public (string Vowel, string Tail) RhymeKey(string word)
    {
        var key = Key(word);
        if (Phones(key) is not { } phones)
        {
            return SpellingKey(key);
        }

        var vowels = Enumerable.Range(0, phones.Count).Where(i => IsVowelPhone(phones[i])).ToList();
        if (vowels.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        var stressed = vowels.LastOrDefault(i => phones[i][^1] == '1', vowels.LastOrDefault(i => phones[i][^1] == '2', vowels[^1]));
        return (Strip(phones[stressed]), string.Join(' ', phones.Skip(stressed + 1).Select(Strip)));
    }

    private IReadOnlyList<string>? Phones(string key)
    {
        foreach (var form in Forms(key))
        {
            if (lexicon.Pronunciations(form) is [var first, ..])
            {
                return first;
            }
        }

        return null;
    }

    private static IEnumerable<string> Forms(string key)
    {
        yield return key;
        if (key.EndsWith("in'", StringComparison.Ordinal))
        {
            yield return key[..^1] + "g";
        }

        if (key.Contains('\'', StringComparison.Ordinal))
        {
            yield return key.Replace("'", string.Empty, StringComparison.Ordinal);
        }
    }

    private int? Compound(string key)
    {
        var plain = key.EndsWith("in'", StringComparison.Ordinal) ? key[..^1] + "g" : key;
        for (var split = MinCompoundPart; split <= plain.Length - MinCompoundPart; split++)
        {
            if (Phones(plain[..split]) is { } head && Phones(plain[split..]) is { } tail)
            {
                return head.Count(IsVowelPhone) + tail.Count(IsVowelPhone);
            }
        }

        return null;
    }

    private static int Rule(string key)
    {
        var letters = key.Replace("'", string.Empty, StringComparison.Ordinal);
        var groups = 0;
        for (var i = 0; i < letters.Length; i++)
        {
            if (IsVowelLetter(letters, i) && (i == 0 || !IsVowelLetter(letters, i - 1)))
            {
                groups++;
            }
        }

        var silentE = groups > 1 && letters.EndsWith('e') && !letters.EndsWith("le", StringComparison.Ordinal) && !letters.EndsWith("ee", StringComparison.Ordinal);
        return Math.Max(groups - (silentE ? 1 : 0), letters.Length > 0 ? 1 : 0);
    }

    private static (string Vowel, string Tail) SpellingKey(string key)
    {
        var letters = key.Replace("'", string.Empty, StringComparison.Ordinal);
        var last = Enumerable.Range(0, letters.Length).LastOrDefault(i => IsVowelLetter(letters, i), -1);
        if (last < 0)
        {
            return (string.Empty, string.Empty);
        }

        var start = last;
        while (start > 0 && IsVowelLetter(letters, start - 1))
        {
            start--;
        }

        return (letters[start..(last + 1)], letters[(last + 1)..]);
    }

    private static bool IsVowelLetter(string letters, int i) =>
        letters[i] is 'a' or 'e' or 'i' or 'o' or 'u' || (letters[i] == 'y' && i > 0);

    private static bool IsVowelPhone(string phone) => phone.Length > 0 && char.IsDigit(phone[^1]);

    private static string Strip(string phone) => phone.TrimEnd('0', '1', '2');

    private static string Key(string? word) => (word ?? string.Empty).Trim().Replace('’', '\'').ToLower(CultureInfo.InvariantCulture);
}

/// <summary>How two words rhyme.</summary>
public enum RhymeKind
{
    /// <summary>Equal rhyme part.</summary>
    Perfect,

    /// <summary>Same vowel with another ending, or the same ending with another vowel.</summary>
    Near,
}

/// <summary>Rhyme kinds from two rhyme keys (research R-04).</summary>
public static class RhymeMatch
{
    /// <summary>Perfect when the keys are equal; near when the vowel is equal (other tail) or the tail is equal and not empty (other vowel).</summary>
    /// <param name="a">First key.</param>
    /// <param name="b">Second key.</param>
    /// <returns>The kind, or null for no rhyme.</returns>
    public static RhymeKind? Kind((string Vowel, string Tail) a, (string Vowel, string Tail) b)
    {
        if (a.Vowel.Length == 0 || b.Vowel.Length == 0)
        {
            return null;
        }

        if (a == b)
        {
            return RhymeKind.Perfect;
        }

        return a.Vowel == b.Vowel || (a.Tail.Length > 0 && a.Tail == b.Tail) ? RhymeKind.Near : null;
    }
}
