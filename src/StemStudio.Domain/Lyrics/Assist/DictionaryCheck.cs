namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>A word the dictionaries, the personal list and the licence rules did not accept (passed to the model when it runs).</summary>
/// <param name="Line">1-based line.</param>
/// <param name="Start">UTF-16 offset in the line.</param>
/// <param name="Word">The word as written.</param>
/// <param name="Suggestions">Dictionary corrections, best first.</param>
public sealed record WordMiss(int Line, int Start, string Word, IReadOnlyList<string> Suggestions);

/// <summary>Result of the dictionary layer.</summary>
/// <param name="Notes">Recognised licences (rules layer; shown at strict only).</param>
/// <param name="Misses">Remaining unknown words.</param>
public sealed record DictionaryCheckResult(IReadOnlyList<SuggestionDraft> Notes, IReadOnlyList<WordMiss> Misses);

/// <summary>
/// The deterministic layer of a check (research R-03 steps 1-5): markers and blank lines are skipped, personal words accepted, the
/// licence lexicon, form rules, code-switch and sound-spelling rules recognise licences; what remains is a miss.
/// </summary>
/// <param name="spelling">Spell checker.</param>
/// <param name="personalWords">The personal words of the sheet language.</param>
public sealed class DictionaryCheck(ISpellChecker spelling, IReadOnlyCollection<string> personalWords)
{
    /// <summary>Dictionary suggestions per miss.</summary>
    public const int MaxCorrections = 5;

    private static readonly HashSet<string> Interjections = new(
        ["oh", "ooh", "oeh", "ah", "aah", "hey", "na", "nah", "la", "lala", "nana", "yeah", "yeh", "whoa", "wo", "woah", "uh", "mm", "hmm", "jee", "tralala", "oho", "ha", "haha"],
        StringComparer.Ordinal);

    /// <summary>Checks the lines of a text.</summary>
    /// <param name="text">Normalised text.</param>
    /// <param name="language">Language.</param>
    /// <param name="lineFrom">First line or null.</param>
    /// <param name="lineTo">Last line or null.</param>
    /// <returns>Licence notes and misses.</returns>
    public DictionaryCheckResult Run(string text, AssistLanguage language, int? lineFrom = null, int? lineTo = null)
    {
        var lines = SheetText.Lines(text);
        var notes = new List<SuggestionDraft>();
        var misses = new List<WordMiss>();
        for (var line = Math.Max(lineFrom ?? 1, 1); line <= Math.Min(lineTo ?? lines.Length, lines.Length); line++)
        {
            if (SheetText.IsLyric(lines[line - 1]))
            {
                CheckLine(line, lines[line - 1], language, notes, misses);
            }
        }

        return new DictionaryCheckResult(notes, misses);
    }

    /// <summary>The suggestion of a miss when no model classifies it: an improvement, never an error (R-03 step 5).</summary>
    /// <param name="miss">The miss.</param>
    /// <param name="language">Language of the explanation.</param>
    /// <returns>The draft.</returns>
    public static SuggestionDraft Unknown(WordMiss miss, AssistLanguage language)
    {
        ArgumentNullException.ThrowIfNull(miss);
        var best = miss.Suggestions.Count > 0 ? miss.Suggestions[0] : null;
        return new SuggestionDraft
        {
            Type = SuggestionType.SpellingUnknownWord, Severity = SuggestionSeverity.Improvement, Anchor = new Anchor(miss.Line, miss.Start, miss.Word.Length, miss.Word),
            Operation = best is null ? SuggestionOperation.None : SuggestionOperation.Replace, Proposal = best,
            Explanation = AssistTexts.Unknown(language, miss.Suggestions), Source = SuggestionSource.Dictionary,
        };
    }

    /// <summary>A typo of a miss confirmed by the model.</summary>
    /// <param name="miss">The miss.</param>
    /// <param name="correction">The model's correction.</param>
    /// <param name="language">Language.</param>
    /// <param name="model">Model name.</param>
    /// <returns>The draft.</returns>
    public static SuggestionDraft Typo(WordMiss miss, string correction, AssistLanguage language, string model)
    {
        ArgumentNullException.ThrowIfNull(miss);
        return new SuggestionDraft
        {
            Type = SuggestionType.SpellingTypo, Severity = SuggestionSeverity.Error, Anchor = new Anchor(miss.Line, miss.Start, miss.Word.Length, miss.Word),
            Operation = SuggestionOperation.Replace, Proposal = correction, Explanation = AssistTexts.Typo(language, correction), Source = SuggestionSource.Model, Model = model,
        };
    }

    /// <summary>A licence note of a miss (rules or model).</summary>
    /// <param name="miss">The miss.</param>
    /// <param name="type">Licence type.</param>
    /// <param name="language">Language.</param>
    /// <param name="source">Layer.</param>
    /// <param name="model">Model name for the model layer.</param>
    /// <returns>The draft.</returns>
    public static SuggestionDraft Licence(WordMiss miss, SuggestionType type, AssistLanguage language, SuggestionSource source, string? model = null)
    {
        ArgumentNullException.ThrowIfNull(miss);
        return Note(miss.Line, new LyricToken(miss.Start, miss.Word), type, language) with { Source = source, Model = model };
    }

    private void CheckLine(int line, string text, AssistLanguage language, List<SuggestionDraft> notes, List<WordMiss> misses)
    {
        foreach (var token in LyricTokens.Of(text).Where(token => !token.Text.All(char.IsDigit) && !IsPersonal(token.Text)))
        {
            if (Classify(token.Text, language) is { } licence)
            {
                notes.Add(Note(line, token, licence, language));
            }
            else if (!spelling.IsKnown(token.Text, language))
            {
                misses.Add(new WordMiss(line, token.Start, token.Text, spelling.Suggest(token.Text, language, MaxCorrections)));
            }
        }
    }

    private SuggestionType? Classify(string word, AssistLanguage language)
    {
        if (LicenceLexicon.TypeOf(word, language) is { } lexicon)
        {
            return lexicon;
        }

        if (spelling.IsKnown(word, language))
        {
            return null;
        }

        return LicenceLexicon.Recognise(word, language, spelling)
            ?? (spelling.IsKnown(word, Other(language)) ? SuggestionType.LicenceCodeSwitch : (SuggestionType?)null)
            ?? (IsSoundSpelling(word, language) ? SuggestionType.LicenceSoundSpelling : (SuggestionType?)null);
    }

    private bool IsSoundSpelling(string word, AssistLanguage language)
    {
        var key = LyricTokens.Key(word);
        if (Interjections.Contains(key))
        {
            return true;
        }

        return Collapse(key, 2) != key && new[] { Collapse(key, 2), Collapse(key, 1) }.Any(form => Interjections.Contains(form) || spelling.IsKnown(form, language));
    }

    private static string Collapse(string key, int keep)
    {
        var builder = new System.Text.StringBuilder();
        var run = 0;
        for (var i = 0; i < key.Length; i++)
        {
            run = i > 0 && key[i] == key[i - 1] ? run + 1 : 1;
            if (run <= keep)
            {
                builder.Append(key[i]);
            }
        }

        return builder.ToString();
    }

    private bool IsPersonal(string word) =>
        personalWords.Contains(word) || (word.Length > 0 && char.IsUpper(word[0]) && personalWords.Contains(char.ToLowerInvariant(word[0]) + word[1..]));

    private static SuggestionDraft Note(int line, LyricToken token, SuggestionType type, AssistLanguage language) => new()
    {
        Type = type, Severity = SuggestionSeverity.Idea, Anchor = new Anchor(line, token.Start, token.Text.Length, token.Text),
        Operation = SuggestionOperation.None, Explanation = AssistTexts.Licence(language, type), Source = SuggestionSource.Rules,
    };

    private static AssistLanguage Other(AssistLanguage language) => language == AssistLanguage.Dutch ? AssistLanguage.English : AssistLanguage.Dutch;
}

/// <summary>Explanations of the deterministic layers in the sheet's language (owner decision Q3).</summary>
public static class AssistTexts
{
    /// <summary>An unknown word.</summary>
    /// <param name="language">Language.</param>
    /// <param name="suggestions">Dictionary corrections.</param>
    /// <returns>The explanation.</returns>
    public static string Unknown(AssistLanguage language, IReadOnlyList<string> suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        var list = string.Join(", ", suggestions.Take(3));
        return language == AssistLanguage.Dutch
            ? suggestions.Count > 0 ? $"Het woordenboek kent dit woord niet. Bedoel je: {list}?" : "Het woordenboek kent dit woord niet."
            : suggestions.Count > 0 ? $"The dictionary does not know this word. Did you mean: {list}?" : "The dictionary does not know this word.";
    }

    /// <summary>A confirmed typo.</summary>
    /// <param name="language">Language.</param>
    /// <param name="correction">Correction.</param>
    /// <returns>The explanation.</returns>
    public static string Typo(AssistLanguage language, string correction) =>
        language == AssistLanguage.Dutch ? $"Spelfout; bedoeld is waarschijnlijk \"{correction}\"." : $"Spelling mistake; probably \"{correction}\".";

    /// <summary>A recognised licence.</summary>
    /// <param name="language">Language.</param>
    /// <param name="type">Licence type.</param>
    /// <returns>The explanation.</returns>
    public static string Licence(AssistLanguage language, SuggestionType type) => (language, type) switch
    {
        (AssistLanguage.Dutch, SuggestionType.LicenceElision) => "Dichterlijke vrijheid: weglating (spreektaal). Geen fout.",
        (AssistLanguage.Dutch, SuggestionType.LicenceArchaism) => "Dichterlijke vrijheid: ouderwets woord. Geen fout.",
        (AssistLanguage.Dutch, SuggestionType.LicenceCodeSwitch) => "Dichterlijke vrijheid: woord uit een andere taal. Geen fout.",
        (AssistLanguage.Dutch, SuggestionType.LicenceSoundSpelling) => "Dichterlijke vrijheid: gespeld zoals het klinkt. Geen fout.",
        (AssistLanguage.Dutch, _) => "Dichterlijke vrijheid: informele taal. Geen fout.",
        (_, SuggestionType.LicenceElision) => "Poetic licence: elision (spoken form). Not a mistake.",
        (_, SuggestionType.LicenceArchaism) => "Poetic licence: archaic word. Not a mistake.",
        (_, SuggestionType.LicenceCodeSwitch) => "Poetic licence: a word of another language. Not a mistake.",
        (_, SuggestionType.LicenceSoundSpelling) => "Poetic licence: spelled as it sounds. Not a mistake.",
        _ => "Poetic licence: informal language. Not a mistake.",
    };
}
