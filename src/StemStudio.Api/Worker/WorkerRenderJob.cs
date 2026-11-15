using System.Text.Json.Serialization;

namespace StemStudio.Api.Worker;

/// <summary>Output file format of a render (worker contract 1.4.0 <c>RenderFormat</c>).</summary>
public enum WorkerRenderFormat
{
    /// <summary>FLAC.</summary>
    Flac,

    /// <summary>WAV.</summary>
    Wav,

    /// <summary>MP3.</summary>
    Mp3,
}

/// <summary>One input of a render (contract <c>RenderInput</c>).</summary>
/// <param name="Path">File relative to the library root.</param>
/// <param name="Gain">Gain 0..1.</param>
/// <param name="Pan">Pan -1..1.</param>
public sealed record WorkerRenderInput(string Path, double Gain, double Pan);

/// <summary>Time range of a render (contract <c>RenderRange</c>).</summary>
/// <param name="StartSeconds">Start.</param>
/// <param name="EndSeconds">End.</param>
public sealed record WorkerRenderRange(double StartSeconds, double EndSeconds);

/// <summary>Output settings (contract <c>RenderOutput</c>); unset values use the worker defaults.</summary>
/// <param name="Format">File format.</param>
public sealed record WorkerRenderOutput(WorkerRenderFormat Format)
{
    /// <summary>16 or 24 (FLAC/WAV); omitted when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BitDepth { get; init; }

    /// <summary>192, 256 or 320 (MP3); omitted when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Mp3BitrateKbps { get; init; }

    /// <summary>TPDF dither for integer formats; false for raw single-input renders.</summary>
    public bool Dither { get; init; } = true;
}

/// <summary>Tags written into the file (contract <c>RenderMetadata</c>, each at most 200 characters).</summary>
/// <param name="Title">Title.</param>
/// <param name="Artist">Artist.</param>
/// <param name="Comment">Comment (the contents text).</param>
public sealed record WorkerRenderMetadata(string? Title, string? Artist, string? Comment);

/// <summary>Body of <c>POST /v1/render-jobs</c> (contract <c>RenderJobRequest</c>).</summary>
public sealed record WorkerRenderJobRequest
{
    /// <summary>Inputs (1..64).</summary>
    public required IReadOnlyList<WorkerRenderInput> Inputs { get; init; }

    /// <summary>Master gain 0..1.</summary>
    public required double MasterGain { get; init; }

    /// <summary>Range, or null for the whole length.</summary>
    public WorkerRenderRange? Range { get; init; }

    /// <summary>Output settings.</summary>
    public required WorkerRenderOutput Output { get; init; }

    /// <summary>File tags.</summary>
    public WorkerRenderMetadata? Metadata { get; init; }
}

/// <summary>Render job state (contract <c>RenderJobStatus</c>).</summary>
public enum WorkerRenderJobState
{
    /// <summary>Waiting.</summary>
    Queued,

    /// <summary>Rendering.</summary>
    Running,

    /// <summary>Terminal: file available.</summary>
    Completed,

    /// <summary>Terminal: see the error.</summary>
    Failed,

    /// <summary>Terminal: cancelled by a delete.</summary>
    Cancelled,
}

/// <summary>The rendered file (contract <c>RenderResult</c>).</summary>
/// <param name="FileName">File name, <c>output.&lt;ext&gt;</c>.</param>
/// <param name="ContentType">Media type.</param>
/// <param name="SizeBytes">Size.</param>
/// <param name="Sha256">SHA-256 of the file.</param>
/// <param name="DurationSeconds">Duration.</param>
/// <param name="SampleRate">Sample rate.</param>
/// <param name="Channels">Channel count.</param>
/// <param name="PeakDbfs">Peak in dBFS.</param>
/// <param name="AppliedGainDb">Gain applied against clipping.</param>
public sealed record WorkerRenderResult(
    string FileName, string ContentType, long SizeBytes, string Sha256, double DurationSeconds, int SampleRate, int Channels, double PeakDbfs, double AppliedGainDb);

/// <summary>A render job (contract <c>RenderJob</c>).</summary>
public sealed record WorkerRenderJob
{
    /// <summary>Job ID (32 lower-case hex characters).</summary>
    public required string RenderJobId { get; init; }

    /// <summary>State.</summary>
    public required WorkerRenderJobState Status { get; init; }

    /// <summary>Progress 0..1.</summary>
    public double Progress { get; init; }

    /// <summary>Readable reason when failed.</summary>
    public string? Error { get; init; }

    /// <summary>The file once completed.</summary>
    public WorkerRenderResult? Result { get; init; }
}
