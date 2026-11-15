namespace StemStudio.Domain.Lyrics;

/// <summary>One word; times (ms from the track start) and origin are null in a plain document.</summary>
/// <param name="Text">Text (1..100 characters).</param>
/// <param name="StartMs">Start.</param>
/// <param name="EndMs">End (&gt; start, at most start + 3000).</param>
/// <param name="Origin">Where the time comes from; null in a plain document.</param>
/// <param name="LowConfidence">Aligned word with a low probability.</param>
public sealed record LyricsWord(string Text, int? StartMs, int? EndMs, WordOrigin? Origin, bool LowConfidence);

/// <summary>One line with its words; times are null in a plain document.</summary>
/// <param name="StartMs">Start.</param>
/// <param name="EndMs">End.</param>
/// <param name="Text">Text (1..500 characters).</param>
/// <param name="Words">Words in order (1..40).</param>
public sealed record LyricsLine(int? StartMs, int? EndMs, string Text, IReadOnlyList<LyricsWord> Words);

/// <summary>
/// One normalised lyrics document (feature 006, data-model 2.1). Copyrighted text: stored per track, shown in the app
/// only, never logged or exported. <see cref="LyricsDocumentRules"/> holds the invariants.
/// </summary>
public sealed record LyricsDocument
{
    /// <summary>Longest text in characters.</summary>
    public const int MaxTextLength = 20_000;

    private static readonly char[] WordSeparators = [' ', '\t'];

    /// <summary>Best timing.</summary>
    public SyncLevel SyncLevel { get; init; }

    /// <summary>Dominant word origin (<see cref="LyricsOrigin.None"/> for plain documents).</summary>
    public LyricsOrigin Origin { get; init; }

    /// <summary>ISO 639 language code, when known.</summary>
    public string? Language { get; init; }

    /// <summary>Where the text comes from.</summary>
    public LyricsSource Source { get; init; }

    /// <summary>LRCLIB record id; null for user text.</summary>
    public string? SourceId { get; init; }

    /// <summary>Duration of the LRCLIB record.</summary>
    public double? SourceDurationSeconds { get; init; }

    /// <summary>The song is instrumental (no lines).</summary>
    public bool Instrumental { get; init; }

    /// <summary>Lines ordered by start (at most 400).</summary>
    public IReadOnlyList<LyricsLine> Lines { get; init; } = [];

    /// <summary>The text as received, newlines normalised (used for re-alignment).</summary>
    public string PlainText { get; init; } = string.Empty;

    /// <summary>Builds the plain document of text entered by the user: one line per non-blank line, words split at spaces and tabs.</summary>
    /// <param name="text">The text (1..20,000 characters; no control characters except line breaks and tabs).</param>
    /// <param name="language">ISO 639 language hint, or null.</param>
    /// <returns>The validated document (source user).</returns>
    /// <exception cref="DomainValidationException">The text or the language breaks a rule.</exception>
    public static LyricsDocument FromUserText(string text, string? language)
    {
        var normalised = CheckUserText(text);
        var document = new LyricsDocument
        {
            SyncLevel = SyncLevel.Plain,
            Origin = LyricsOrigin.None,
            Language = language,
            Source = LyricsSource.User,
            Lines = PlainLines(normalised),
            PlainText = normalised,
        };
        LyricsDocumentRules.Validate(document);
        return document;
    }

    /// <summary>Normalises the line breaks, trims, and checks the text rules.</summary>
    private static string CheckUserText(string? raw)
    {
        var text = (raw ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        var errors = new ValidationErrors();
        errors.Check(text.Length > 0, "text", "Enter at least one line of lyrics.");
        errors.Check(text.Length <= MaxTextLength, "text", $"Must be at most {MaxTextLength} characters.");
        errors.Check(!text.Any(IsForbiddenControl), "text", "Must not contain control characters other than line breaks and tabs.");
        errors.ThrowIfAny();
        return text;
    }

    private static bool IsForbiddenControl(char value) => char.IsControl(value) && value is not ('\n' or '\t');

    private static List<LyricsLine> PlainLines(string text) =>
        text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).Select(PlainLine).ToList();

    private static LyricsLine PlainLine(string line) =>
        new(null, null, line, line.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries).Select(word => new LyricsWord(word, null, null, null, false)).ToList());
}
