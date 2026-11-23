using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Worker;

/// <summary>Device record of a worker job or detail job (worker contract 1.2.0 <c>JobDevice</c>); not yet validated.</summary>
/// <param name="Kind">GPU or CPU.</param>
/// <param name="Name">GPU name when the kind is GPU.</param>
/// <param name="CpuFallback">Whether a model step fell back to the CPU.</param>
/// <param name="FallbackReason">Reason of the latest fallback.</param>
public sealed record WorkerJobDevice(ProcessingDeviceKind Kind, string? Name, bool CpuFallback, string? FallbackReason);

/// <summary>Active processing device of the worker, probed once at start-up (worker contract 1.2.0 <c>DeviceInfo</c>).</summary>
public sealed record WorkerDeviceInfo
{
    /// <summary>GPU or CPU.</summary>
    public required ProcessingDeviceKind Kind { get; init; }

    /// <summary>GPU name; null on the CPU.</summary>
    public string? Name { get; init; }

    /// <summary>GPU compute capability, e.g. <c>12.0</c>.</summary>
    public string? ComputeCapability { get; init; }

    /// <summary>Total GPU memory in MiB.</summary>
    public int? TotalMemoryMiB { get; init; }

    /// <summary>Free GPU memory at start-up in MiB.</summary>
    public int? FreeMemoryMiB { get; init; }

    /// <summary>Why the CPU is used although a GPU was requested; null otherwise.</summary>
    public string? Reason { get; init; }
}

/// <summary>Body of the worker's <c>GET /health</c> (worker contract 1.2.0 <c>Health</c>).</summary>
public sealed record WorkerHealth
{
    /// <summary>Always <c>ok</c>.</summary>
    public required string Status { get; init; }

    /// <summary><c>demucs</c> or <c>fake</c>.</summary>
    public required string Separator { get; init; }

    /// <summary>Configured device preference: <c>cpu</c>, <c>cuda</c> or <c>auto</c>.</summary>
    public required string Device { get; init; }

    /// <summary>The device actually used.</summary>
    public required WorkerDeviceInfo DeviceInfo { get; init; }

    /// <summary>Worker contract version, e.g. <c>1.2.0</c>.</summary>
    public required string ContractVersion { get; init; }

    /// <summary>Lyrics alignment readiness (1.5.0); null for an older worker.</summary>
    public WorkerLyricsAlignmentHealth? LyricsAlignment { get; init; }

    /// <summary>Readiness of the transcription models (1.6.0); null for an older worker.</summary>
    public WorkerTranscriptionHealth? Transcription { get; init; }

    /// <summary>Queue state of the lanes beside separation (1.8.0 adds <c>practice</c>); null for an older worker.</summary>
    public WorkerLanesHealth? Lanes { get; init; }

    /// <summary>The configured section analyser (1.8.0); null for an older worker.</summary>
    public WorkerStructureHealth? Structure { get; init; }

    /// <summary>Engines and model readiness of the instrument-analysis lane (worker 1.9.0, feature 016); null before 1.9.0.</summary>
    public WorkerInstrumentAnalysisHealth? InstrumentAnalysis { get; init; }

    /// <summary>Analyzer and handout renderer of the insights lane (worker 1.11.0, feature 020); null before 1.11.0.</summary>
    public WorkerInsightsHealth? Insights { get; init; }
}

/// <summary>Readiness of the transcription models (worker 1.6.0 <c>TranscriptionHealth</c>).</summary>
/// <param name="Models">Model id -> <c>ready</c> or <c>not-downloaded</c>.</param>
public sealed record WorkerTranscriptionHealth(IReadOnlyDictionary<string, string> Models)
{
    /// <summary>Model id of the piano CRNN.</summary>
    public const string PianoModel = "piano-crnn-note-pedal";

    /// <summary>True when the piano model is downloaded.</summary>
    public bool PianoReady => Models.TryGetValue(PianoModel, out var state) && state == "ready";
}
