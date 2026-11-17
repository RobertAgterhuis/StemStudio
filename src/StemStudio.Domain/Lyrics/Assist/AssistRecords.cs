using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>A stored earlier text of a sheet (data-model 2.2; the last 50 per sheet are kept).</summary>
public sealed class LyricsSheetRevision
{
    /// <summary>Revisions kept per sheet.</summary>
    public const int Retention = 50;

    private LyricsSheetRevision()
    {
        Title = string.Empty;
        Text = string.Empty;
    }

    /// <summary>Sheet.</summary>
    public Guid SheetId { get; private set; }

    /// <summary>Revision number.</summary>
    public int Revision { get; private set; }

    /// <summary>Title snapshot.</summary>
    public string Title { get; private set; }

    /// <summary>Text snapshot.</summary>
    public string Text { get; private set; }

    /// <summary>Cause.</summary>
    public RevisionCause Cause { get; private set; }

    /// <summary>The accept action that produced it.</summary>
    public Guid? AcceptGroupId { get; private set; }

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>The current state of a sheet as a revision.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="cause">Cause.</param>
    /// <param name="acceptGroupId">Accept action, if any.</param>
    /// <param name="nowUtc">Current time.</param>
    /// <returns>The revision.</returns>
    public static LyricsSheetRevision Of(LyricsSheet sheet, RevisionCause cause, Guid? acceptGroupId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return new LyricsSheetRevision
        {
            SheetId = sheet.Id, Revision = sheet.Revision, Title = sheet.Title, Text = sheet.Text, Cause = cause, AcceptGroupId = acceptGroupId, CreatedAt = nowUtc,
        };
    }
}

/// <summary>A word accepted as correct in all sheets of a language (data-model 5).</summary>
public sealed class LyricWord
{
    /// <summary>Most words per language.</summary>
    public const int MaxPerLanguage = 5000;

    /// <summary>Longest word.</summary>
    public const int MaxLength = 40;

    private LyricWord()
    {
        Word = string.Empty;
    }

    /// <summary>Identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Language.</summary>
    public AssistLanguage Language { get; private set; }

    /// <summary>The word (NFC, no spaces).</summary>
    public string Word { get; private set; }

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Creates a word.</summary>
    /// <param name="id">Identifier.</param>
    /// <param name="language">Language.</param>
    /// <param name="word">The word (normalised here).</param>
    /// <param name="nowUtc">Current time.</param>
    /// <returns>The word.</returns>
    /// <exception cref="DomainValidationException">The word is empty, too long or has spaces.</exception>
    public static LyricWord Create(Guid id, AssistLanguage language, string word, DateTime nowUtc)
    {
        var normalized = Normalize(word);
        var errors = new ValidationErrors();
        errors.Check(normalized.Length is > 0 and <= MaxLength && !normalized.Any(char.IsWhiteSpace), "word", "A word is 1 to 40 characters without spaces.");
        errors.ThrowIfAny();
        return new LyricWord { Id = id, Language = language, Word = normalized, CreatedAt = nowUtc };
    }

    /// <summary>NFC and trimmed.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The normalised word.</returns>
    public static string Normalize(string? word) => (word ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);
}

/// <summary>Global lyric-assistant settings (one row, data-model 7).</summary>
public sealed class LyricAssistSettings
{
    /// <summary>The fixed id of the single row.</summary>
    public const int SingletonId = 1;

    /// <summary>Row id (always 1).</summary>
    public int Id { get; private set; } = SingletonId;

    /// <summary>When the Claude notice was acknowledged.</summary>
    public DateTime? ClaudeNoticeAcknowledgedAt { get; private set; }

    /// <summary>The notice version acknowledged.</summary>
    public string? ClaudeNoticeVersion { get; private set; }

    /// <summary>True when the given notice version is acknowledged.</summary>
    /// <param name="currentVersion">Current notice version.</param>
    /// <returns>True when acknowledged.</returns>
    public bool HasAcknowledged(string currentVersion) => ClaudeNoticeAcknowledgedAt is not null && ClaudeNoticeVersion == currentVersion;

    /// <summary>Acknowledges or withdraws the notice.</summary>
    /// <param name="acknowledge">True to acknowledge.</param>
    /// <param name="version">Notice version.</param>
    /// <param name="nowUtc">Current time.</param>
    public void SetClaudeNotice(bool acknowledge, string version, DateTime nowUtc) =>
        (ClaudeNoticeAcknowledgedAt, ClaudeNoticeVersion) = acknowledge ? ((DateTime?)nowUtc, (string?)version) : (null, null);
}

/// <summary>The user's confirmation that a refused sheet is their own work (data-model 2.3, FR-031).</summary>
public sealed record OwnWorkOverride
{
    /// <summary>Current statement version.</summary>
    public const string CurrentStatementVersion = "own-work.v1";

    /// <summary>The statement text of <see cref="CurrentStatementVersion"/>.</summary>
    public const string StatementText =
        "This text is my own work (for example a song of my own band). I confirm that StemStudio may treat it as my own lyrics although it matches the lyrics of an imported track.";

    /// <summary>Longest name.</summary>
    public const int MaxConfirmedByLength = 80;

    /// <summary>Longest track title snapshot.</summary>
    public const int MaxTrackTitleLength = 200;

    /// <summary>Statement version.</summary>
    public required string StatementVersion { get; init; }

    /// <summary>SHA-256 (hex, lower case) of the statement text.</summary>
    public required string StatementSha256 { get; init; }

    /// <summary>The typed name.</summary>
    public required string ConfirmedBy { get; init; }

    /// <summary>When it was confirmed (UTC).</summary>
    public required DateTime ConfirmedAt { get; init; }

    /// <summary>The matched track.</summary>
    public required Guid MatchedTrackId { get; init; }

    /// <summary>Track title snapshot.</summary>
    public required string MatchedTrackTitle { get; init; }

    /// <summary>SHA-256 of the current statement text.</summary>
    public static string StatementHash { get; } = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(StatementText)));

    /// <summary>Creates an override of the current statement.</summary>
    /// <param name="confirmedBy">The typed name (1-80 after trimming).</param>
    /// <param name="track">The matched track.</param>
    /// <param name="nowUtc">Current time.</param>
    /// <returns>The override.</returns>
    /// <exception cref="DomainValidationException">The name is empty or too long.</exception>
    public static OwnWorkOverride Create(string? confirmedBy, CorpusTrack track, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(track);
        var name = (confirmedBy ?? string.Empty).Trim();
        var errors = new ValidationErrors();
        errors.Check(name.Length is > 0 and <= MaxConfirmedByLength, "confirmedBy", "Type your name (1 to 80 characters).");
        errors.ThrowIfAny();
        return new OwnWorkOverride
        {
            StatementVersion = CurrentStatementVersion, StatementSha256 = StatementHash, ConfirmedBy = name, ConfirmedAt = nowUtc,
            MatchedTrackId = track.TrackId, MatchedTrackTitle = Domain.Text.TrimToLength(track.Title, MaxTrackTitleLength) ?? string.Empty,
        };
    }
}

/// <summary>Per-sheet rhythm target (data-model 2; research R-04).</summary>
/// <param name="Bpm">Beats per minute (40-220).</param>
/// <param name="BeatsPerBar">Beats per bar (2-7).</param>
/// <param name="BarsPerLine">Bars per line (1-8).</param>
/// <param name="SyllablesPerBeat">Syllables per beat (0.5-4.0, step 0.25).</param>
public sealed record RhythmTarget(int Bpm, int BeatsPerBar, int BarsPerLine, double SyllablesPerBeat)
{
    /// <summary>Target syllables per line (rounded half away from zero).</summary>
    public int SyllablesPerLine => (int)Math.Round(BeatsPerBar * BarsPerLine * SyllablesPerBeat, MidpointRounding.AwayFromZero);

    /// <summary>Seconds of one line.</summary>
    public double SecondsPerLine => BarsPerLine * BeatsPerBar * 60.0 / Bpm;

    /// <summary>The errors of the target; empty when valid.</summary>
    /// <param name="field">Field prefix.</param>
    /// <returns>The errors.</returns>
    public IReadOnlyList<DomainError> Validate(string field = "rhythmTarget")
    {
        var errors = new ValidationErrors(field);
        errors.Check(Bpm is >= 40 and <= 220, "bpm", "Must be between 40 and 220.");
        errors.Check(BeatsPerBar is >= 2 and <= 7, "beatsPerBar", "Must be between 2 and 7.");
        errors.Check(BarsPerLine is >= 1 and <= 8, "barsPerLine", "Must be between 1 and 8.");
        errors.Check(SyllablesPerBeat is >= 0.5 and <= 4.0 && Math.Abs((SyllablesPerBeat * 4) - Math.Round(SyllablesPerBeat * 4)) < 1e-9, "syllablesPerBeat",
            string.Create(CultureInfo.InvariantCulture, $"Must be between 0.5 and 4.0 in steps of 0.25."));
        return errors.Errors;
    }
}

/// <summary>One stored event of an assist (SSE replay, data-model 10).</summary>
public sealed class AssistEvent
{
    /// <summary>Largest payload in bytes.</summary>
    public const int MaxPayloadBytes = 16 * 1024;

    private AssistEvent()
    {
        Type = string.Empty;
        Data = string.Empty;
    }

    /// <summary>Assist.</summary>
    public Guid AssistId { get; private set; }

    /// <summary>Sequence number (1-based per assist).</summary>
    public int Seq { get; private set; }

    /// <summary>Event type (<c>assist.started</c>, ...).</summary>
    public string Type { get; private set; }

    /// <summary>JSON payload.</summary>
    public string Data { get; private set; }

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Creates an event.</summary>
    /// <param name="assistId">Assist.</param>
    /// <param name="seq">Sequence number.</param>
    /// <param name="type">Type.</param>
    /// <param name="data">JSON payload.</param>
    /// <param name="nowUtc">Current time.</param>
    /// <returns>The event.</returns>
    public static AssistEvent Create(Guid assistId, int seq, string type, string data, DateTime nowUtc) =>
        new() { AssistId = assistId, Seq = seq, Type = type, Data = data, CreatedAt = nowUtc };
}
