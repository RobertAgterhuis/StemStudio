namespace StemStudio.Domain.Lyrics;

/// <summary>The editable content of a lyrics sheet.</summary>
/// <param name="Title">Title (1-80).</param>
/// <param name="Language">ISO 639-1 code from the supported list, or <c>unknown</c>.</param>
/// <param name="Text">The lyrics (1-4,096 after normalisation).</param>
public sealed record LyricsContent(string Title, string Language, string Text);

/// <summary>The lyrics a song was made from (kept when the sheet changes or is deleted).</summary>
/// <param name="LyricsId">The sheet, if the song used one.</param>
/// <param name="Title">Title.</param>
/// <param name="Language">Language.</param>
/// <param name="Text">Normalised text.</param>
public sealed record LyricsSnapshot(Guid? LyricsId, string Title, string Language, string Text);

/// <summary>Own lyrics saved for songs (feature 010, data-model 3.1); not the synced lyrics of a track (006).</summary>
public sealed partial class LyricsSheet
{
    private LyricsSheet()
    {
        Title = string.Empty;
        Language = string.Empty;
        Text = string.Empty;
    }

    /// <summary>Identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Title.</summary>
    public string Title { get; private set; }

    /// <summary>Language code.</summary>
    public string Language { get; private set; }

    /// <summary>Normalised text.</summary>
    public string Text { get; private set; }

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Creates a sheet from normalised, validated content.</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="content">The content (normalised here).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The sheet.</returns>
    /// <exception cref="DomainValidationException">A field breaks a rule.</exception>
    public static LyricsSheet Create(Guid id, LyricsContent content, DateTime nowUtc)
    {
        var sheet = new LyricsSheet { Id = id, CreatedAt = nowUtc };
        sheet.Update(content, nowUtc);
        sheet.LastCause = Assist.RevisionCause.Created;
        return sheet;
    }

    /// <summary>Replaces the content (a user save: the revision increases, assisted lines are pruned); songs keep their snapshot.</summary>
    /// <param name="content">The content (normalised here).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="DomainValidationException">A field breaks a rule.</exception>
    public void Update(LyricsContent content, DateTime nowUtc)
    {
        var valid = LyricsRules.Valid(content);
        Change(valid, Assist.RevisionCause.UserSave, nowUtc);
    }

    /// <summary>The sheet as a song snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public LyricsSnapshot Snapshot() => new(Id, Title, Language, Text);
}

/// <summary>Rules of lyrics sheets: title 1-80, a supported language, text 1-4,096 after normalisation.</summary>
public static class LyricsRules
{
    /// <summary>Longest title.</summary>
    public const int MaxTitleLength = 80;

    /// <summary>Longest normalised text.</summary>
    public const int MaxTextLength = 4096;

    /// <summary>The vocal languages of ACE-Step (ISO 639-1) plus <c>unknown</c>.</summary>
    public static IReadOnlyList<string> Languages { get; } = EmbeddedText.Lines("languages.v1.txt");

    /// <summary>Normalises the content and returns its errors.</summary>
    /// <param name="content">The content.</param>
    /// <returns>The errors; empty when valid.</returns>
    public static IReadOnlyList<DomainError> Validate(LyricsContent content) => Check(Normalized(content)).Errors;

    /// <summary>The normalised content (title trimmed, text normalised).</summary>
    /// <param name="content">The content.</param>
    /// <returns>The normalised content.</returns>
    public static LyricsContent Normalized(LyricsContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new LyricsContent((content.Title ?? string.Empty).Trim(), (content.Language ?? string.Empty).Trim(), LyricsNormaliser.Normalize(content.Text));
    }

    /// <summary>The normalised content when valid.</summary>
    /// <param name="content">The content.</param>
    /// <returns>The normalised content.</returns>
    /// <exception cref="DomainValidationException">A field breaks a rule.</exception>
    public static LyricsContent Valid(LyricsContent content)
    {
        var normalized = Normalized(content);
        Check(normalized).ThrowIfAny();
        return normalized;
    }

    private static ValidationErrors Check(LyricsContent content)
    {
        var errors = new ValidationErrors();
        errors.Check(content.Title.Length is > 0 and <= MaxTitleLength, "title", "The title must be 1 to 80 characters.");
        errors.Check(Languages.Contains(content.Language), "language", "Choose a supported language or unknown.");
        errors.Check(content.Text.Length is > 0 and <= MaxTextLength, "text", "The lyrics must be 1 to 4,096 characters (after removing timestamps and extra blank lines).");
        return errors;
    }
}
