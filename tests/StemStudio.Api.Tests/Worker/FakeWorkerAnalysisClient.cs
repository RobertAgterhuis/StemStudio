using System.Collections.Concurrent;
using System.Text.Json;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;

namespace StemStudio.Api.Tests.Worker;

/// <summary>
/// In-memory worker analysis lane (worker 1.9.0) for API tests, consumer-driven from the worker examples
/// (<c>analysis-*.json</c>): jobs start queued and the test runs, completes or fails them.
/// </summary>
internal sealed class FakeWorkerAnalysisClient : IWorkerAnalysisClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private int _next;

    /// <summary>When true every call throws <see cref="WorkerUnavailableException"/>.</summary>
    public bool Unavailable { get; set; }

    /// <summary>When set, every submission throws it.</summary>
    public Exception? SubmitFailure { get; set; }

    /// <summary>Awaited inside every submission (to hold two concurrent starts).</summary>
    public Func<Task>? OnSubmit { get; set; }

    /// <summary>Awaited inside every job read (to change the analysis while a sync pass runs).</summary>
    public Func<Task>? OnGet { get; set; }

    public ConcurrentQueue<WorkerAnalysisRequest> Requests { get; } = new();

    public ConcurrentDictionary<string, WorkerAnalysisJob> Jobs { get; } = new();

    public ConcurrentBag<string> Deleted { get; } = [];

    public static WorkerAnalysisJob Example(string name) => JsonSerializer.Deserialize<WorkerAnalysisJob>(WorkerExamples.Read(name), Json)!;

    public static WorkerAnalysisResult Result(string json) => JsonSerializer.Deserialize<WorkerAnalysisResult>(json, Json)!;

    /// <summary>A drum hits result of the 1.9.0 <c>DrumHits</c> schema.</summary>
    public static WorkerAnalysisResult DrumHits() => Result("""
        {"kind": "drum-hits", "engine": "drum-onsets", "pieces": ["kick", "snare"],
         "hits": [{"timeMs": 500, "piece": "kick", "strength": 0.9}, {"timeMs": 1000, "piece": "snare", "strength": 0.6}]}
        """);

    /// <summary>A reference melody result of the 1.9.0 <c>VocalPitch</c> schema.</summary>
    public static WorkerAnalysisResult VocalPitch(string source = "vocals.lead") => Result($$"""
        {"kind": "vocal-pitch", "hopMs": 10, "frames": [0, 5700, 5710, 0], "source": "{{source}}", "engine": "pyin",
         "voicedRatio": 0.5, "range": {"lowCents": 5700, "highCents": 5710}, "warnings": []}
        """);

    public async Task<WorkerAnalysisJob> CreateAnalysisJobAsync(WorkerAnalysisRequest request, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        if (OnSubmit is { } hold)
        {
            await hold();
        }

        if (SubmitFailure is { } failure)
        {
            throw failure;
        }

        Requests.Enqueue(request);
        var id = Interlocked.Increment(ref _next).ToString("x32", System.Globalization.CultureInfo.InvariantCulture);
        var job = new WorkerAnalysisJob { AnalysisJobId = id, Kind = request.Kind, Status = WorkerPracticeJobState.Queued };
        Jobs[id] = job;
        return job;
    }

    public async Task<WorkerAnalysisJob?> GetAnalysisJobAsync(string jobId, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        if (OnGet is { } meanwhile)
        {
            await meanwhile();
        }

        return Jobs.GetValueOrDefault(jobId);
    }

    public Task DeleteAnalysisJobAsync(string jobId, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        Deleted.Add(jobId);
        Jobs.TryRemove(jobId, out _);
        return Task.CompletedTask;
    }

    /// <summary>The worker runs the job.</summary>
    public void Run(string jobId, double progress = 0.42) =>
        Jobs[jobId] = Example("analysis-running-chords-job") with { AnalysisJobId = jobId, Kind = Jobs[jobId].Kind, Progress = progress };

    /// <summary>Completes the job with the given result (default: the completed chords example).</summary>
    public void Complete(string jobId, WorkerAnalysisResult? result = null)
    {
        var done = Example("analysis-completed-chords-job") with { AnalysisJobId = jobId };
        Jobs[jobId] = result is null ? done : done with { Kind = result.Kind, Result = result };
    }

    /// <summary>Fails the job with the failed pitch example's text.</summary>
    public void Fail(string jobId) => Jobs[jobId] = Example("analysis-failed-pitch-job") with { AnalysisJobId = jobId };

    /// <summary>The worker forgets the job (restart).</summary>
    public void Forget(string jobId) => Jobs.TryRemove(jobId, out _);

    private void ThrowIfUnavailable()
    {
        if (Unavailable)
        {
            throw new WorkerUnavailableException("The processing service cannot be reached.");
        }
    }
}
