using StemStudio.Domain.Generations;

namespace StemStudio.Api.Generator;

/// <summary><c>GET /health</c> of a generator or singer service (generator contract 1.1.0 <c>GeneratorHealth</c>).</summary>
/// <param name="Status">Always <c>ok</c> while the process runs.</param>
/// <param name="ContractVersion">Contract version of the service.</param>
/// <param name="Engine"><c>acestep</c>, <c>soulx</c> or <c>fake</c>.</param>
/// <param name="Kinds">Job kinds this instance accepts.</param>
/// <param name="VoiceSampleCheck">Whether the voice-sample check is served (singer).</param>
/// <param name="Ready">True with a usable GPU.</param>
/// <param name="Reason">Why it is not ready.</param>
/// <param name="Device">The probed device.</param>
/// <param name="Models">The model files it needs.</param>
/// <param name="Queue">Queue state.</param>
public sealed record GeneratorHealth(
    string Status,
    string ContractVersion,
    string Engine,
    IReadOnlyList<string> Kinds,
    bool VoiceSampleCheck,
    bool Ready,
    string? Reason,
    GeneratorDevice Device,
    IReadOnlyList<GeneratorModel> Models,
    GeneratorQueueState Queue)
{
    /// <summary>Whether this instance accepts a job kind.</summary>
    /// <param name="kind">The wire kind.</param>
    /// <returns>True when served.</returns>
    public bool Serves(string kind) => Kinds.Contains(kind, StringComparer.Ordinal);
}

/// <summary>The device of a service (same shape and reason texts as worker 1.2.0 <c>DeviceInfo</c>).</summary>
/// <param name="Kind"><c>gpu</c> or <c>cpu</c>.</param>
/// <param name="Name">GPU name.</param>
/// <param name="ComputeCapability">e.g. <c>12.0</c>.</param>
/// <param name="TotalMemoryMiB">Total GPU memory.</param>
/// <param name="FreeMemoryMiB">Free GPU memory.</param>
/// <param name="Reason">Probe reason when no GPU is usable.</param>
public sealed record GeneratorDevice(string Kind, string? Name, string? ComputeCapability, int? TotalMemoryMiB, int? FreeMemoryMiB, string? Reason);

/// <summary>A model file the service needs.</summary>
/// <param name="Id">Model id.</param>
/// <param name="Present">Whether it is downloaded and verified.</param>
/// <param name="Bytes">Download size.</param>
/// <param name="Revision">Pinned revision.</param>
public sealed record GeneratorModel(string Id, bool Present, long Bytes, string Revision);

/// <summary>Queue state.</summary>
/// <param name="Queued">Queued jobs.</param>
/// <param name="Running">The running job, if any.</param>
public sealed record GeneratorQueueState(int Queued, string? Running);

/// <summary>Status of a generator job.</summary>
public enum GeneratorJobStatus
{
    /// <summary>Queued.</summary>
    Queued,

    /// <summary>Waiting for the GPU lease or memory.</summary>
    WaitingForGpu,

    /// <summary>Downloading weights.</summary>
    DownloadingModel,

    /// <summary>Running.</summary>
    Running,

    /// <summary>Completed.</summary>
    Completed,

    /// <summary>Failed.</summary>
    Failed,

    /// <summary>Cancelled.</summary>
    Cancelled,
}

/// <summary>A generator job (<c>Generation</c> of the generator contract); the kind's parameters are not read back.</summary>
public sealed record GeneratorJob
{
    /// <summary>Job id (32 lower-case hex characters).</summary>
    public required string GenerationId { get; init; }

    /// <summary>Kind on the wire (<c>restyle</c>, <c>song</c>, <c>voiceConversion</c>).</summary>
    public required string Kind { get; init; }

    /// <summary>Status.</summary>
    public required GeneratorJobStatus Status { get; init; }

    /// <summary>Progress 0-100.</summary>
    public int Progress { get; init; }

    /// <summary>Readable stage.</summary>
    public string? Stage { get; init; }

    /// <summary>Takes once completed.</summary>
    public IReadOnlyList<GeneratorTake> Takes { get; init; } = [];

    /// <summary>Warnings.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Device once known.</summary>
    public GeneratorJobDevice? Device { get; init; }

    /// <summary>Error iff failed.</summary>
    public GeneratorJobError? Error { get; init; }
}

/// <summary>A take of a completed job.</summary>
/// <param name="Index">Take number from 1.</param>
/// <param name="Seed">Seed.</param>
/// <param name="DurationSeconds">Duration.</param>
/// <param name="SampleRate">Always 48000.</param>
/// <param name="Sha256">SHA-256 of the FLAC file.</param>
/// <param name="Bytes">File size.</param>
/// <param name="PeakDbfs">Sample peak.</param>
/// <param name="Model">The model.</param>
public sealed record GeneratorTake(int Index, int Seed, double DurationSeconds, int SampleRate, string Sha256, long Bytes, double PeakDbfs, GeneratorTakeModel Model)
{
    /// <summary>AI voice label (<c>synthetic</c>/<c>converted</c>, 1.1.0); null for restyles.</summary>
    public string? Vocal { get; init; }

    /// <summary>Whether <c>/vocal</c> serves a converted vocal stem (1.1.0).</summary>
    public bool HasVocalStem { get; init; }

    /// <summary>Song profile (<c>planned</c>/<c>direct</c>, 1.1.0).</summary>
    public string? Profile { get; init; }
}

/// <summary>The model of a take.</summary>
/// <param name="Id">Model id.</param>
/// <param name="Revision">Weights revision.</param>
public sealed record GeneratorTakeModel(string Id, string Revision);

/// <summary>Where a job ran (worker 1.2.0 <c>JobDevice</c> shape).</summary>
/// <param name="Kind"><c>gpu</c> or <c>cpu</c>.</param>
/// <param name="Name">GPU name.</param>
/// <param name="CpuFallback">Always false for the generator.</param>
/// <param name="FallbackReason">Null.</param>
public sealed record GeneratorJobDevice(string Kind, string? Name, bool CpuFallback, string? FallbackReason);

/// <summary>Why a job failed.</summary>
/// <param name="Code">Error code on the wire.</param>
/// <param name="Message">Readable message.</param>
public sealed record GeneratorJobError(string Code, string Message);

/// <summary>Restyle parameters (<c>RestyleParams</c>).</summary>
/// <param name="Caption">Caption (1-512).</param>
/// <param name="AudioCoverStrength">Cover strength 0-1.</param>
/// <param name="InferenceSteps">Steps.</param>
/// <param name="Shift">Shift.</param>
/// <param name="Seeds">One seed per take.</param>
/// <param name="Mode">Range or in place.</param>
/// <param name="RangeStartSeconds">Range start.</param>
/// <param name="RangeEndSeconds">Range end.</param>
public sealed record GeneratorRestyleParams(
    string Caption,
    double AudioCoverStrength,
    int InferenceSteps,
    double Shift,
    IReadOnlyList<int> Seeds,
    RestyleMode Mode,
    double RangeStartSeconds,
    double RangeEndSeconds);

/// <summary>A take to download.</summary>
/// <param name="GenerationId">Job id.</param>
/// <param name="Index">Take number (1-2).</param>
/// <param name="Sha256">Expected SHA-256 (64 lower-case hex characters).</param>
public sealed record GeneratorTakeRef(string GenerationId, int Index, string Sha256);

/// <summary>A stem file sent to the generator.</summary>
/// <param name="Name">Stem name (part file name <c>&lt;name&gt;.flac</c>).</param>
/// <param name="FilePath">Absolute path of the FLAC file.</param>
public sealed record GeneratorStemFile(string Name, string FilePath);

/// <summary>A restyle job request: the parameters plus 1-6 stems.</summary>
/// <param name="Params">The parameters.</param>
/// <param name="Stems">The stems.</param>
public sealed record GeneratorRestyleRequest(GeneratorRestyleParams Params, IReadOnlyList<GeneratorStemFile> Stems);

/// <summary>Machine-readable reason of a refused request (<c>GeneratorProblemCode</c>).</summary>
public enum GeneratorProblemCode
{
    /// <summary>Invalid parameters or files.</summary>
    InvalidInput,

    /// <summary>No usable GPU.</summary>
    GpuRequired,

    /// <summary>The source is shorter than the range.</summary>
    SourceTooShort,

    /// <summary>The instance does not serve the kind (1.1.0).</summary>
    KindNotServed,
}
