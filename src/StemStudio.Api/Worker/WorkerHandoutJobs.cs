using StemStudio.Contracts;
using StemStudio.Domain.Handouts;

namespace StemStudio.Api.Worker;

/// <summary>The fixed notice texts of a handout (worker 1.11.0 <c>HandoutNotice</c>, data-model 6.6; the worker rejects others).</summary>
/// <param name="Footer">Footer on every page.</param>
/// <param name="Full">Full notice on the last page.</param>
public sealed record WorkerHandoutNotice(string Footer, string Full)
{
    /// <summary>The only accepted texts.</summary>
    public static WorkerHandoutNotice Fixed { get; } = new(ContractLimits.HandoutNoticeFooter, ContractLimits.HandoutNoticeFull);
}

/// <summary>One setlist entry (worker 1.11.0 <c>SetlistEntryBlock</c>).</summary>
/// <param name="Position">1-based position.</param>
/// <param name="Title">Song title.</param>
/// <param name="Artist">Artist, or null.</param>
/// <param name="Key">Key text, or null.</param>
/// <param name="TempoBpm">Rounded tempo, or null.</param>
/// <param name="DurationSeconds">Length, or null.</param>
/// <param name="Notes">Entry notes, or null.</param>
public sealed record WorkerSetlistEntryBlock(int Position, string Title, string? Artist, string? Key, int? TempoBpm, double? DurationSeconds, string? Notes);

/// <summary>The setlist block of a setlist handout (worker 1.11.0 <c>SetlistBlock</c>).</summary>
/// <param name="Name">Setlist name.</param>
/// <param name="Notes">Setlist notes, or null.</param>
/// <param name="Entries">Entries in running order.</param>
public sealed record WorkerSetlistBlock(string Name, string? Notes, IReadOnlyList<WorkerSetlistEntryBlock> Entries);

/// <summary>Song facts (worker 1.11.0 <c>SongFacts</c>).</summary>
/// <param name="TempoBpm">Rounded tempo, or null.</param>
/// <param name="TimeSignature"><c>4/4</c>, or null.</param>
/// <param name="Key">Key text (<c>A minor</c>), or null.</param>
/// <param name="KeySource"><c>chords</c>, <c>score</c> or null.</param>
/// <param name="DurationSeconds">Length.</param>
/// <param name="TranspositionNote">Transposition note, or null.</param>
public sealed record WorkerSongFacts(int? TempoBpm, string? TimeSignature, string? Key, string? KeySource, double DurationSeconds, string? TranspositionNote);

/// <summary>A form-chart section (worker 1.11.0 <c>HandoutSection</c>).</summary>
/// <param name="Name">Name (<c>Chorus 2</c>).</param>
/// <param name="StartSeconds">Start.</param>
/// <param name="EndSeconds">End.</param>
/// <param name="StartBar">1-based start bar (0 = pickup), or null.</param>
/// <param name="Bars">Bars, or null.</param>
/// <param name="ChordBars">Chord symbols per bar, or null.</param>
/// <param name="PracticeTempoPercent">Best practice tempo, or null.</param>
public sealed record WorkerHandoutSection(
    string Name, double StartSeconds, double EndSeconds, int? StartBar, int? Bars, IReadOnlyList<IReadOnlyList<string>>? ChordBars, int? PracticeTempoPercent);

/// <summary>A chord above a lyric line (worker 1.11.0 <c>LyricChord</c>).</summary>
/// <param name="Symbol">Chord symbol (ASCII accidentals).</param>
/// <param name="CharIndex">Character index in the line.</param>
public sealed record WorkerLyricChord(string Symbol, int CharIndex);

/// <summary>A lyric line with chords (worker 1.11.0 <c>LyricLine</c>); empty text = instrumental chord row.</summary>
/// <param name="Text">Lyric text.</param>
/// <param name="Section">Index into the sections, or null.</param>
/// <param name="Chords">Chords.</param>
public sealed record WorkerLyricLine(string Text, int? Section, IReadOnlyList<WorkerLyricChord> Chords);

/// <summary>Lyrics of a handout song (worker 1.11.0 <c>HandoutLyrics</c>).</summary>
/// <param name="Lines">Lines.</param>
/// <param name="ChordPlacement"><c>word</c>, <c>line</c> or <c>none</c>.</param>
/// <param name="Source">Lyrics source text.</param>
public sealed record WorkerHandoutLyrics(IReadOnlyList<WorkerLyricLine> Lines, string ChordPlacement, string Source);

/// <summary>A notation PDF of a handout song (worker 1.11.0 <c>HandoutNotation</c>).</summary>
/// <param name="Title">Title (<c>Bass - tablature</c>).</param>
/// <param name="Kind">Kind.</param>
/// <param name="PdfPath">Library path <c>assets/&lt;id&gt;/score.pdf</c>.</param>
public sealed record WorkerHandoutNotation(string Title, NotationKind Kind, string PdfPath);

/// <summary>One song of a handout (worker 1.11.0 <c>HandoutSong</c>).</summary>
public sealed record WorkerHandoutSong
{
    /// <summary>Title.</summary>
    public required string Title { get; init; }

    /// <summary>Artist, or null.</summary>
    public required string? Artist { get; init; }

    /// <summary>Library path of the cover, or null.</summary>
    public required string? ArtworkPath { get; init; }

    /// <summary>Facts.</summary>
    public required WorkerSongFacts Facts { get; init; }

    /// <summary>Sections (at most 64).</summary>
    public required IReadOnlyList<WorkerHandoutSection> Sections { get; init; }

    /// <summary>Lyrics, or null.</summary>
    public required WorkerHandoutLyrics? Lyrics { get; init; }

    /// <summary>Distinct chord symbols in order of appearance (at most 64).</summary>
    public required IReadOnlyList<string> ChordSymbols { get; init; }

    /// <summary>Notation PDFs (at most 8).</summary>
    public required IReadOnlyList<WorkerHandoutNotation> Notation { get; init; }

    /// <summary>The family's practice notes, or null.</summary>
    public required string? PracticeNotes { get; init; }

    /// <summary>Setlist entry notes, or null.</summary>
    public required string? EntryNotes { get; init; }

    /// <summary>What the renderer prints.</summary>
    public required IReadOnlyList<IngredientCode> Include { get; init; }
}

/// <summary>A complete, resolved handout document (worker 1.11.0 <c>HandoutDocument</c>, data-model 2.5).</summary>
public sealed record WorkerHandoutDocument
{
    /// <summary>Song or setlist.</summary>
    public required HandoutKind Kind { get; init; }

    /// <summary>Family.</summary>
    public required HandoutFamily Family { get; init; }

    /// <summary>Family label (at most 60 characters).</summary>
    public required string FamilyLabel { get; init; }

    /// <summary>Song title or setlist name.</summary>
    public required string Title { get; init; }

    /// <summary>Artist or "Setlist - 12 songs", or null.</summary>
    public required string? Subtitle { get; init; }

    /// <summary>Date (<c>YYYY-MM-DD</c>).</summary>
    public required string GeneratedOn { get; init; }

    /// <summary>The fixed notice texts.</summary>
    public required WorkerHandoutNotice Notice { get; init; }

    /// <summary>Set iff <see cref="Kind"/> is setlist.</summary>
    public required WorkerSetlistBlock? Setlist { get; init; }

    /// <summary>Songs (1 for a song, 1..50 for a setlist).</summary>
    public required IReadOnlyList<WorkerHandoutSong> Songs { get; init; }

    /// <summary>Attributions (at most 20).</summary>
    public required IReadOnlyList<string> Attributions { get; init; }

    /// <summary>App version (at most 40 characters).</summary>
    public required string AppVersion { get; init; }
}

/// <summary>Body of <c>POST /v1/handout-jobs</c> (worker 1.11.0 <c>HandoutJobRequest</c>).</summary>
/// <param name="Document">The document.</param>
/// <param name="Paper">Paper.</param>
public sealed record WorkerHandoutJobRequest(WorkerHandoutDocument Document, Paper Paper);

/// <summary>Result of a handout job (worker 1.11.0 <c>HandoutResult</c>).</summary>
/// <param name="FileName">Always <c>handout.pdf</c>.</param>
/// <param name="ContentType">Always <c>application/pdf</c>.</param>
/// <param name="SizeBytes">PDF size.</param>
/// <param name="Sha256">PDF SHA-256.</param>
/// <param name="Pages">Page count.</param>
/// <param name="Outline">Bookmark titles.</param>
/// <param name="Warnings">Warnings.</param>
public sealed record WorkerHandoutResult(string FileName, string ContentType, long SizeBytes, string Sha256, int Pages, IReadOnlyList<string> Outline, IReadOnlyList<HandoutWarning> Warnings);

/// <summary>A handout job (worker 1.11.0 <c>HandoutJob</c>).</summary>
/// <param name="HandoutJobId">Job id (32 hex).</param>
/// <param name="Status">Status.</param>
/// <param name="Stage"><c>layout</c>, <c>notation</c>, <c>writing</c> or null.</param>
/// <param name="Progress">Progress 0..1.</param>
/// <param name="Error">Failure text.</param>
/// <param name="Result">Result when completed.</param>
public sealed record WorkerHandoutJob(string HandoutJobId, WorkerRenderJobState Status, string? Stage, double Progress, string? Error, WorkerHandoutResult? Result);
