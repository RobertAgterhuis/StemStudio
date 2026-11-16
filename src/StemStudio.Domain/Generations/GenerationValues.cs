using StemStudio.Domain.Styles;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Generations;

/// <summary>Kind of a generation (009: restyle; 010: song, voice conversion).</summary>
public enum GenerationKind
{
    /// <summary>A track restyled with a style (feature 009).</summary>
    Restyle,

    /// <summary>A song from lyrics (feature 010).</summary>
    Song,

    /// <summary>A song take with its vocal converted to a voice profile (feature 010).</summary>
    VoiceConversion,
}

/// <summary>Status of a generation (009 data-model 2.4 mirrored; <see cref="WaitingForStems"/> is API-only, 010).</summary>
public enum GenerationState
{
    /// <summary>Created, not running yet.</summary>
    Queued,

    /// <summary>A voice conversion waits for the separation of its source take (010).</summary>
    WaitingForStems,

    /// <summary>Waiting for the GPU lease or free memory.</summary>
    WaitingForGpu,

    /// <summary>Downloading model weights.</summary>
    DownloadingModel,

    /// <summary>Generating.</summary>
    Running,

    /// <summary>Takes are available.</summary>
    Completed,

    /// <summary>Failed; see the error.</summary>
    Failed,

    /// <summary>Cancelled by the user.</summary>
    Cancelled,
}

/// <summary>Machine-readable failure reason (generator contract 1.1.0).</summary>
public enum GenerationErrorCode
{
    /// <summary>No usable GPU.</summary>
    GpuRequired,

    /// <summary>Not enough free GPU memory within the wait limit.</summary>
    GpuBusy,

    /// <summary>GPU out of memory (also after the low-memory retry).</summary>
    OutOfMemory,

    /// <summary>The model download failed.</summary>
    ModelDownloadFailed,

    /// <summary>The generator restarted while the job ran.</summary>
    Interrupted,

    /// <summary>The input was refused.</summary>
    InvalidInput,

    /// <summary>The engine failed.</summary>
    EngineError,

    /// <summary>The vocal stem has no singing (010).</summary>
    UnvoicedTarget,

    /// <summary>The voice sample is unusable (010).</summary>
    PromptInvalid,
}

/// <summary>How a restyle uses its range.</summary>
public enum RestyleMode
{
    /// <summary>The range becomes the take.</summary>
    Range,

    /// <summary>The whole track is kept and only the range is restyled.</summary>
    InPlace,
}

/// <summary>A failure with a readable message.</summary>
/// <param name="Code">The code.</param>
/// <param name="Message">The message (at most 500 characters).</param>
public sealed record GenerationError(GenerationErrorCode Code, string Message);

/// <summary>The part of the source a restyle uses.</summary>
/// <param name="StartSeconds">Start.</param>
/// <param name="EndSeconds">End.</param>
/// <param name="Mode">Range or in place.</param>
public sealed record SourceRange(double StartSeconds, double EndSeconds, RestyleMode Mode)
{
    /// <summary>Length of the range.</summary>
    public double LengthSeconds => EndSeconds - StartSeconds;
}

/// <summary>The model that made a take.</summary>
/// <param name="Id">Model id, e.g. <c>acestep-v15-turbo</c>.</param>
/// <param name="Revision">Weights revision.</param>
public sealed record TakeModel(string Id, string Revision);

/// <summary>A non-terminal status report of the generator.</summary>
/// <param name="State">The status (not terminal).</param>
/// <param name="Progress">Progress 0-100.</param>
/// <param name="Stage">Readable stage.</param>
/// <param name="Device">Device once known.</param>
/// <param name="Warnings">Warnings so far.</param>
public sealed record GenerationReport(GenerationState State, int Progress, string? Stage, JobDevice? Device, IReadOnlyList<string> Warnings);

/// <summary>What a restyle was made from (snapshots kept when the track or style is deleted).</summary>
public sealed record RestyleDetails
{
    /// <summary>The source track; null after it was deleted.</summary>
    public Guid? SourceTrackId { get; init; }

    /// <summary>Title snapshot of the source track.</summary>
    public required string SourceTrackTitle { get; init; }

    /// <summary>The range.</summary>
    public required SourceRange Range { get; init; }

    /// <summary>The stems sent to the generator.</summary>
    public required IReadOnlyList<string> IncludedStems { get; init; }

    /// <summary>The style at creation.</summary>
    public required StyleSnapshot Style { get; init; }

    /// <summary>The strength step.</summary>
    public required Strength Strength { get; init; }

    /// <summary>The caption sent to the model.</summary>
    public required string Caption { get; init; }

    /// <summary>The cover strength sent to the model.</summary>
    public required double AudioCoverStrength { get; init; }

    /// <inheritdoc />
    public bool Equals(RestyleDetails? other) =>
        other is not null
        && (SourceTrackId, SourceTrackTitle, Range, Style, Strength, Caption, AudioCoverStrength)
            == (other.SourceTrackId, other.SourceTrackTitle, other.Range, other.Style, other.Strength, other.Caption, other.AudioCoverStrength)
        && IncludedStems.SequenceEqual(other.IncludedStems, StringComparer.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(SourceTrackId, SourceTrackTitle, Range, Caption);
}
