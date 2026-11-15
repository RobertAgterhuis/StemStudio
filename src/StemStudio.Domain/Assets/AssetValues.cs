using StemStudio.Domain.Rendering;
using StemStudio.Domain.Scores;

namespace StemStudio.Domain.Assets;

/// <summary>Kind of a library item (feature 007; 008 adds score).</summary>
public enum AssetKind
{
    /// <summary>An audio file.</summary>
    Audio,

    /// <summary>Sheet music (feature 008).</summary>
    Score,
}

/// <summary>Status of a library item (data-model 4.5).</summary>
public enum AssetStatus
{
    /// <summary>Waiting for the worker.</summary>
    Queued,

    /// <summary>The worker renders it.</summary>
    Rendering,

    /// <summary>Files are available.</summary>
    Ready,

    /// <summary>Rendering failed; see the error. Retryable.</summary>
    Failed,
}

/// <summary>How a library item was made.</summary>
public enum AssetOrigin
{
    /// <summary>Exported from the stems of a track (feature 007).</summary>
    Export,

    /// <summary>Transcribed to sheet music (feature 008).</summary>
    Transcription,
}

/// <summary>A stored file of a ready item under <c>assets/&lt;id&gt;/</c>.</summary>
/// <param name="Role">File role, e.g. <c>audio</c>.</param>
/// <param name="FileName">Name on disk, <c>&lt;role&gt;.&lt;ext&gt;</c>.</param>
/// <param name="ContentType">Media type.</param>
/// <param name="SizeBytes">Size in bytes.</param>
/// <param name="Sha256">SHA-256 of the content (64 lower-case hex characters).</param>
public sealed record AssetFile(string Role, string FileName, string ContentType, long SizeBytes, string Sha256);

/// <summary>Facts of a rendered audio file.</summary>
/// <param name="Format">File format.</param>
/// <param name="BitDepth">16 or 24 for FLAC/WAV, else null.</param>
/// <param name="Mp3BitrateKbps">Bit rate of an MP3, else null.</param>
/// <param name="DurationSeconds">Duration.</param>
/// <param name="PeakDbfs">Peak of the file in dBFS (&lt;= 0).</param>
/// <param name="AppliedGainDb">Gain applied to avoid clipping (0 or negative).</param>
public sealed record AudioFacts(AudioFormat Format, int? BitDepth, int? Mp3BitrateKbps, double DurationSeconds, double PeakDbfs, double AppliedGainDb)
{
    /// <summary>Sample rate (always 44.1 kHz).</summary>
    public int SampleRate { get; init; } = 44_100;

    /// <summary>Channel count (always stereo).</summary>
    public int Channels { get; init; } = 2;
}

/// <summary>Where a library item came from (data-model 4.1); a snapshot taken at creation.</summary>
public sealed record Provenance
{
    /// <summary>How it was made.</summary>
    public required AssetOrigin Origin { get; init; }

    /// <summary>The source track, or null.</summary>
    public Guid? TrackId { get; init; }

    /// <summary>Title of the track at creation (&lt;= 300 characters).</summary>
    public required string TrackTitle { get; init; }

    /// <summary>Artist of the track at creation.</summary>
    public string? TrackArtist { get; init; }

    /// <summary>Library items this item was made from (empty for exports).</summary>
    public IReadOnlyList<Guid> SourceAssetIds { get; init; } = [];

    /// <summary>Short text such as "drums, raw" (&lt;= 200 characters).</summary>
    public required string Contents { get; init; }

    /// <summary>How an export was made (origin export); null for score items (feature 008).</summary>
    public ExportRecipe? Recipe { get; init; }

    /// <summary>How a score item was made (origin transcription, feature 008); null for exports.</summary>
    public ScoreRecipe? ScoreRecipe { get; init; }
}
