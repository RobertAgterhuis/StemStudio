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

    /// <summary>A take of a generation (feature 009).</summary>
    Generation,

    /// <summary>A play-along take (feature 015): hidden from the library list, managed by its take.</summary>
    Recording,
}

/// <summary>The take a recording item holds (feature 015, <c>Provenance.recording</c>).</summary>
/// <param name="TakeId">The take.</param>
public sealed record RecordingSource(Guid TakeId)
{
    /// <summary>The transient upload the FLAC is rendered from.</summary>
    public const string CaptureFile = "capture.wav";
}

/// <summary>A stored generated take for the library (feature 009).</summary>
/// <param name="Name">Display name.</param>
/// <param name="Files">The stored files.</param>
/// <param name="Audio">Audio facts.</param>
public sealed record GeneratedAudio(string Name, IReadOnlyList<AssetFile> Files, AudioFacts Audio);

/// <summary>The generation and take a library item came from (feature 009).</summary>
/// <param name="GenerationId">The generation.</param>
/// <param name="TakeIndex">The take number.</param>
/// <param name="Kind">The generation kind.</param>
public sealed record GenerationSource(Guid GenerationId, int TakeIndex, Generations.GenerationKind Kind);

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

    /// <summary>The generation of a take item (origin generation, feature 009); null otherwise.</summary>
    public GenerationSource? Generation { get; init; }

    /// <summary>The take of a recording item (origin recording, feature 015); null otherwise.</summary>
    public RecordingSource? Recording { get; init; }
}
