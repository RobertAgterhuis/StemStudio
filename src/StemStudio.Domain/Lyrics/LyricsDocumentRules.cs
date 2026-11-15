using System.Text.RegularExpressions;

namespace StemStudio.Domain.Lyrics;

/// <summary>
/// Invariants of a <see cref="LyricsDocument"/> (data-model 2.1), checked again on worker data: timing all set (word,
/// line) or all null (plain); <c>endMs &gt; startMs</c>; words inside their line; starts never decrease; a word lasts at
/// most 3000 ms; an instrumental document has no lines; size limits. Messages never quote the lyrics.
/// </summary>
public static partial class LyricsDocumentRules
{
    /// <summary>Most lines of a document.</summary>
    public const int MaxLines = 400;

    /// <summary>Longest line text.</summary>
    public const int MaxLineLength = 500;

    /// <summary>Most words of a line.</summary>
    public const int MaxWords = 40;

    /// <summary>Longest word text.</summary>
    public const int MaxWordLength = 100;

    /// <summary>Longest word in milliseconds.</summary>
    public const int MaxWordMs = 3000;

    /// <summary>Longest LRCLIB record id.</summary>
    public const int MaxSourceIdLength = 64;

    /// <summary>Validates a document.</summary>
    /// <param name="document">The document.</param>
    /// <exception cref="DomainValidationException">A rule is violated.</exception>
    public static void Validate(LyricsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var errors = new ValidationErrors();
        CheckDocument(errors, document);
        var timed = document.SyncLevel != SyncLevel.Plain;
        var previousStart = 0;
        for (var index = 0; index < document.Lines.Count; index++)
        {
            previousStart = CheckLine(errors.Nested($"lines[{index}]"), document.Lines[index], timed, previousStart);
        }

        errors.ThrowIfAny();
    }

    private static void CheckDocument(ValidationErrors errors, LyricsDocument document)
    {
        errors.Check(document.Language is null || LanguagePattern().IsMatch(document.Language), "language", "Must be an ISO 639 code of 2 or 3 lower-case letters.");
        errors.Check((document.SourceId?.Length ?? 0) <= MaxSourceIdLength, "sourceId", $"Must be at most {MaxSourceIdLength} characters.");
        CheckShape(errors, document);
    }

    private static void CheckShape(ValidationErrors errors, LyricsDocument document)
    {
        errors.Check(document.PlainText.Length <= LyricsDocument.MaxTextLength, "plainText", $"Must be at most {LyricsDocument.MaxTextLength} characters.");
        errors.Check(document.Lines.Count <= MaxLines && !(document.Instrumental && document.Lines.Count > 0), "lines", $"Must be at most {MaxLines} lines, and none for an instrumental song.");
        errors.Check((document.SyncLevel == SyncLevel.Plain) == (document.Origin == LyricsOrigin.None), "origin", "Must be none exactly for a plain document.");
    }

    /// <summary>Checks one line and its words; returns the start the next line must not undercut.</summary>
    private static int CheckLine(ValidationErrors errors, LyricsLine line, bool timed, int previousStart)
    {
        errors.Check(IsText(line.Text, MaxLineLength), "text", $"Must be 1 to {MaxLineLength} characters.");
        errors.Check(line.Words.Count is > 0 and <= MaxWords, "words", $"Must be 1 to {MaxWords} words.");
        if (!timed)
        {
            errors.Check(line.StartMs is null && line.EndMs is null, "startMs", "Must be null in a plain document.");
            CheckPlainWords(errors, line.Words);
            return previousStart;
        }

        return CheckTimedLine(errors, line, previousStart);
    }

    private static int CheckTimedLine(ValidationErrors errors, LyricsLine line, int previousStart)
    {
        if (line.StartMs is not { } start || line.EndMs is not { } end)
        {
            errors.Check(false, "startMs", "Must be set in a timed document.");
            return previousStart;
        }

        errors.Check(start >= previousStart, "startMs", "Must not start before the previous line.");
        errors.Check(end > start, "endMs", "Must end after it starts.");
        CheckTimedWords(errors, line.Words, new LineSpan(start, end));
        return Math.Max(start, previousStart);
    }

    private static void CheckTimedWords(ValidationErrors errors, IReadOnlyList<LyricsWord> words, LineSpan line)
    {
        var previousStart = line.Start;
        for (var index = 0; index < words.Count; index++)
        {
            previousStart = CheckTimedWord(errors.Nested($"words[{index}]"), words[index], line, previousStart);
        }
    }

    private static int CheckTimedWord(ValidationErrors errors, LyricsWord word, LineSpan line, int previousStart)
    {
        errors.Check(IsText(word.Text, MaxWordLength), "text", $"Must be 1 to {MaxWordLength} characters.");
        errors.Check(word.Origin is not null, "origin", "Must be set in a timed document.");
        if (word.StartMs is not { } start || word.EndMs is not { } end)
        {
            errors.Check(false, "startMs", "Must be set in a timed document.");
            return previousStart;
        }

        errors.Check(start >= Math.Max(previousStart, line.Start), "startMs", "Must lie inside the line and not before the previous word.");
        errors.Check(end > start && end <= line.End && end - start <= MaxWordMs, "endMs", $"Must end after it starts, inside the line, within {MaxWordMs} ms.");
        return Math.Max(start, previousStart);
    }

    private static void CheckPlainWords(ValidationErrors errors, IReadOnlyList<LyricsWord> words)
    {
        for (var index = 0; index < words.Count; index++)
        {
            var (word, nested) = (words[index], errors.Nested($"words[{index}]"));
            nested.Check(IsText(word.Text, MaxWordLength), "text", $"Must be 1 to {MaxWordLength} characters.");
            nested.Check(word.StartMs is null && word.EndMs is null, "startMs", "Must be null in a plain document.");
            nested.Check(word.Origin is null, "origin", "Must be null in a plain document.");
        }
    }

    private static bool IsText(string? text, int maxLength) => !string.IsNullOrWhiteSpace(text) && text.Length <= maxLength;

    [GeneratedRegex("^[a-z]{2,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex LanguagePattern();

    private readonly record struct LineSpan(int Start, int End);
}
