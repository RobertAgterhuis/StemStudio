using System.Text;

namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>A word, phrase or line the checker must respect in one sheet (data-model 2.1, FR-011).</summary>
public sealed class IntentionalMark
{
    /// <summary>Most marks per sheet.</summary>
    public const int MaxPerSheet = 200;

    /// <summary>Longest note.</summary>
    public const int MaxNoteLength = 120;

    private IntentionalMark()
    {
        Text = string.Empty;
    }

    /// <summary>Identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Scope.</summary>
    public IntentionalScope Scope { get; private set; }

    /// <summary>Normalised text.</summary>
    public string Text { get; private set; }

    /// <summary>Optional note.</summary>
    public string? Note { get; private set; }

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Creates a mark from validated input.</summary>
    /// <param name="id">Identifier.</param>
    /// <param name="scope">Scope.</param>
    /// <param name="text">Text (normalised here).</param>
    /// <param name="note">Optional note.</param>
    /// <param name="nowUtc">Current time.</param>
    /// <returns>The mark.</returns>
    /// <exception cref="DomainValidationException">The text or note breaks a rule.</exception>
    public static IntentionalMark Create(Guid id, IntentionalScope scope, string text, string? note, DateTime nowUtc)
    {
        var normalized = IntentionalMarkMatcher.Normalize(text);
        var errors = new ValidationErrors();
        errors.Check(IntentionalMarkMatcher.IsValidText(scope, normalized), "text", IntentionalMarkMatcher.TextRule(scope));
        errors.Check((note?.Trim().Length ?? 0) <= MaxNoteLength, "note", "The note can be at most 120 characters.");
        errors.ThrowIfAny();
        return new IntentionalMark { Id = id, Scope = scope, Text = normalized, Note = Domain.Text.TrimToLength(note, MaxNoteLength), CreatedAt = nowUtc };
    }
}

/// <summary>Matching of intentional marks (data-model 2.1): word case-sensitive and whole-word, phrase and line exact.</summary>
public static class IntentionalMarkMatcher
{
    /// <summary>Longest word mark.</summary>
    public const int MaxWordLength = 40;

    /// <summary>Longest phrase mark.</summary>
    public const int MaxPhraseLength = 200;

    /// <summary>Longest line mark.</summary>
    public const int MaxLineLength = 400;

    /// <summary>NFC, trimmed, single spaces.</summary>
    /// <param name="text">Text.</param>
    /// <returns>The normalised text.</returns>
    public static string Normalize(string? text) =>
        string.Join(' ', (text ?? string.Empty).Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>True when a normalised text fits the scope.</summary>
    /// <param name="scope">Scope.</param>
    /// <param name="text">Normalised text.</param>
    /// <returns>True when valid.</returns>
    public static bool IsValidText(IntentionalScope scope, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return scope switch
        {
            IntentionalScope.Word => text.Length is > 0 and <= MaxWordLength && !text.Contains(' ', StringComparison.Ordinal),
            IntentionalScope.Phrase => text.Length is > 0 and <= MaxPhraseLength,
            _ => text.Length is > 0 and <= MaxLineLength,
        };
    }

    /// <summary>The readable rule of a scope.</summary>
    /// <param name="scope">Scope.</param>
    /// <returns>The message.</returns>
    public static string TextRule(IntentionalScope scope) => scope switch
    {
        IntentionalScope.Word => "A word mark is 1 to 40 characters without spaces.",
        IntentionalScope.Phrase => "A phrase mark is 1 to 200 characters.",
        _ => "A line mark is 1 to 400 characters.",
    };

    /// <summary>True when the mark's text occurs in the sheet.</summary>
    /// <param name="mark">The mark.</param>
    /// <param name="text">Normalised sheet text.</param>
    /// <returns>True when it occurs.</returns>
    public static bool Occurs(IntentionalMark mark, string text)
    {
        ArgumentNullException.ThrowIfNull(mark);
        return SheetText.Lines(text).Any(line => Spans(mark, line).Any());
    }

    /// <summary>True when the anchor text equals or lies inside an occurrence of the mark (the suggestion is dropped).</summary>
    /// <param name="mark">The mark.</param>
    /// <param name="lineText">The text of the anchor's line.</param>
    /// <param name="anchor">The anchor.</param>
    /// <returns>True when covered.</returns>
    public static bool Covers(IntentionalMark mark, string lineText, Anchor anchor)
    {
        ArgumentNullException.ThrowIfNull(mark);
        ArgumentNullException.ThrowIfNull(anchor);
        return !anchor.IsTitle && Spans(mark, lineText).Any(span => span.Start <= anchor.Start && anchor.End <= span.End);
    }

    private static IEnumerable<(int Start, int End)> Spans(IntentionalMark mark, string line) => mark.Scope switch
    {
        IntentionalScope.Line => Normalize(line) == mark.Text ? [(0, line.Length)] : [],
        IntentionalScope.Word => SheetText.Occurrences(line, mark.Text).Where(start => IsWholeWord(line, start, mark.Text.Length)).Select(start => (start, start + mark.Text.Length)),
        _ => SheetText.Occurrences(line, mark.Text).Select(start => (start, start + mark.Text.Length)),
    };

    private static bool IsWholeWord(string line, int start, int length) =>
        (start == 0 || !IsWordChar(line[start - 1])) && (start + length == line.Length || !IsWordChar(line[start + length]));

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '\'' or '’' or '-';
}
