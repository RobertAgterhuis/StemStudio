namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>One rhyme of the finder.</summary>
/// <param name="Word">The rhyming word.</param>
/// <param name="Kind">Perfect or near.</param>
/// <param name="Syllables">Its syllables.</param>
public sealed record FoundRhyme(string Word, RhymeKind Kind, int Syllables);

/// <summary>
/// Perfect and near rhymes from local word lists (FR-016, research R-04): an index from rhyme key to words is built once per language,
/// so a query is a few dictionary lookups (the 500 ms budget of SC-007).
/// </summary>
/// <param name="words">Candidate words.</param>
/// <param name="phonology">Rhyme keys and syllables.</param>
public sealed class RhymeFinder(IWordList words, LyricPhonology phonology)
{
    /// <summary>Default number of rhymes.</summary>
    public const int DefaultLimit = 30;

    /// <summary>Largest number of rhymes.</summary>
    public const int MaxLimit = 60;

    private readonly Dictionary<AssistLanguage, Lazy<RhymeIndex>> _indexes = new()
    {
        [AssistLanguage.Dutch] = new(() => new RhymeIndex(words.Words(AssistLanguage.Dutch), AssistLanguage.Dutch, phonology)),
        [AssistLanguage.English] = new(() => new RhymeIndex(words.Words(AssistLanguage.English), AssistLanguage.English, phonology)),
    };

    /// <summary>Rhymes of a word: perfect ones first, then near ones, each by fewer syllables and alphabetically.</summary>
    /// <param name="word">The word.</param>
    /// <param name="language">Language.</param>
    /// <param name="limit">Maximum (1-60).</param>
    /// <returns>The rhymes.</returns>
    public IReadOnlyList<FoundRhyme> Find(string word, AssistLanguage language, int limit = DefaultLimit)
    {
        var key = phonology.RhymeKey(word, language);
        if (key.Vowel.Length == 0)
        {
            return [];
        }

        var self = LyricTokens.Key(word);
        var max = Math.Clamp(limit, 1, MaxLimit);
        var index = _indexes[language].Value;
        var perfect = Ranked(index.Perfect(key).Where(w => w != self), RhymeKind.Perfect, language, max);
        var near = Ranked(index.Near(key).Where(w => w != self && !perfect.Any(p => p.Word == w)).Distinct(StringComparer.Ordinal), RhymeKind.Near, language, max - perfect.Count);
        return [.. perfect, .. near];
    }

    // Shorter words first as a cheap proxy before counting syllables, so a frequent vowel never counts thousands of words.
    private List<FoundRhyme> Ranked(IEnumerable<string> words, RhymeKind kind, AssistLanguage language, int take)
    {
        const int Candidates = 400;
        return take <= 0
            ? []
            : [.. words.OrderBy(w => w.Length).ThenBy(w => w, StringComparer.Ordinal).Take(Candidates)
                .Select(w => new FoundRhyme(w, kind, phonology.WordSyllables(w, language)))
                .OrderBy(r => r.Syllables).ThenBy(r => r.Word, StringComparer.Ordinal).Take(take)];
    }

    private sealed class RhymeIndex
    {
        private readonly Dictionary<(string, string), List<string>> _byKey = [];
        private readonly Dictionary<string, List<string>> _byVowel = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _byTail = new(StringComparer.Ordinal);

        public RhymeIndex(IEnumerable<string> words, AssistLanguage language, LyricPhonology phonology)
        {
            foreach (var word in words)
            {
                var key = phonology.RhymeKey(word, language);
                if (key.Vowel.Length > 0)
                {
                    Add(_byKey, key, word);
                    Add(_byVowel, key.Vowel, word);
                    if (key.Tail.Length > 0)
                    {
                        Add(_byTail, key.Tail, word);
                    }
                }
            }
        }

        public List<string> Perfect((string Vowel, string Tail) key) => _byKey.TryGetValue(key, out var list) ? list : [];

        public IEnumerable<string> Near((string Vowel, string Tail) key)
        {
            var sameVowel = _byVowel.TryGetValue(key.Vowel, out var vowels) ? vowels : [];
            var sameTail = key.Tail.Length > 0 && _byTail.TryGetValue(key.Tail, out var tails) ? tails : [];
            return sameVowel.Concat(sameTail);
        }

        private static void Add<TKey>(Dictionary<TKey, List<string>> index, TKey key, string word)
            where TKey : notnull
        {
            if (!index.TryGetValue(key, out var list))
            {
                index[key] = list = [];
            }

            list.Add(word);
        }
    }
}

/// <summary>Deterministic Dutch/English guess for a sheet with language <c>unknown</c> (dictionary hit ratio, FR-001).</summary>
public static class LanguageGuesser
{
    /// <summary>Lowest hit ratio for a guess.</summary>
    public const double MinHitRate = 0.6;

    /// <summary>Most words looked at.</summary>
    public const int MaxWords = 400;

    /// <summary>The guess.</summary>
    /// <param name="text">The text.</param>
    /// <param name="spelling">Spell checker.</param>
    /// <returns>The language (null when neither reaches 60 %) and the confidence (the winner's hit ratio).</returns>
    public static (AssistLanguage? Language, double Confidence) Guess(string text, ISpellChecker spelling)
    {
        ArgumentNullException.ThrowIfNull(spelling);
        var tokens = SheetText.Lines(text ?? string.Empty).Where(SheetText.IsLyric).SelectMany(LyricTokens.Of)
            .Select(t => t.Text).Where(w => w.Length <= DictionaryCheck.MaxWordLength && !w.All(char.IsDigit)).Take(MaxWords).ToList();
        if (tokens.Count == 0)
        {
            return (null, 0);
        }

        var dutch = Rate(tokens, spelling, AssistLanguage.Dutch);
        var english = Rate(tokens, spelling, AssistLanguage.English);
        var (best, rate) = dutch >= english ? (AssistLanguage.Dutch, dutch) : (AssistLanguage.English, english);
        return (rate >= MinHitRate && dutch != english ? best : null, Math.Round(rate, 3));
    }

    private static double Rate(List<string> tokens, ISpellChecker spelling, AssistLanguage language) =>
        (double)tokens.Count(t => spelling.IsKnown(t, language) || LicenceLexicon.TypeOf(t, language) is not null) / tokens.Count;
}
