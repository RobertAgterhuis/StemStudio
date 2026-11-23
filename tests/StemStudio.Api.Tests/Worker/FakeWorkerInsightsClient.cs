using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;

namespace StemStudio.Api.Tests.Worker;

/// <summary>
/// In-memory worker insights lane (worker 1.11.0) for API tests, consumer-driven from the worker examples (<c>quality-*</c>,
/// <c>handout-*</c>, <c>health-insights</c>): jobs start queued and the test completes or fails them.
/// </summary>
internal sealed class FakeWorkerInsightsClient : IWorkerInsightsClient
{
    private static readonly JsonSerializerOptions Json = CreateJson();

    private int _next;

    /// <summary>When true every call throws <see cref="WorkerUnavailableException"/> (worker down).</summary>
    public bool Unavailable { get; set; }

    /// <summary>When set, every job submission throws it (e.g. a <see cref="WorkerRequestException"/> 422).</summary>
    public Exception? SubmitFailure { get; set; }

    /// <summary>When set, the handout file download throws it.</summary>
    public Exception? DownloadFailure { get; set; }

    /// <summary>Calls in order (<c>create-quality</c>, <c>get-handout:&lt;id&gt;</c>, ...).</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    public ConcurrentQueue<WorkerQualityJobRequest> QualityRequests { get; } = new();

    public ConcurrentQueue<WorkerHandoutJobRequest> HandoutRequests { get; } = new();

    public ConcurrentDictionary<string, WorkerQualityJob> QualityJobs { get; } = new();

    public ConcurrentDictionary<string, WorkerHandoutJob> HandoutJobs { get; } = new();

    public ConcurrentBag<string> Deleted { get; } = [];

    /// <summary>The PDF bytes of a handout.</summary>
    public byte[] Pdf { get; set; } = Encoding.ASCII.GetBytes("%PDF-1.7 fake handout");

    /// <summary>The insights health; null = a worker before 1.11.0.</summary>
    public WorkerInsightsHealth? Health { get; set; } = Example<WorkerHealth>("health-insights").Insights;

    /// <summary>When true the health call throws <see cref="WorkerUnavailableException"/> (only the health).</summary>
    public bool HealthUnavailable { get; set; }

    public static T Example<T>(string name) => JsonSerializer.Deserialize<T>(WorkerExamples.Read(name), Json)!;

    public Task<WorkerQualityJob> CreateQualityJobAsync(WorkerQualityJobRequest request, CancellationToken cancellationToken)
    {
        Submit("create-quality");
        QualityRequests.Enqueue(request);
        var job = Example<WorkerQualityJob>("quality-queued-job") with { QualityJobId = NewId() };
        QualityJobs[job.QualityJobId] = job;
        return Task.FromResult(job);
    }

    public Task<WorkerQualityJob?> GetQualityJobAsync(string jobId, CancellationToken cancellationToken)
    {
        Record($"get-quality:{jobId}");
        return Task.FromResult(QualityJobs.GetValueOrDefault(jobId));
    }

    public Task DeleteQualityJobAsync(string jobId, CancellationToken cancellationToken) => Delete($"quality:{jobId}", () => QualityJobs.TryRemove(jobId, out _));

    public Task<WorkerHandoutJob> CreateHandoutJobAsync(WorkerHandoutJobRequest request, CancellationToken cancellationToken)
    {
        Submit("create-handout");
        HandoutRequests.Enqueue(request);
        var job = Example<WorkerHandoutJob>("handout-queued-job") with { HandoutJobId = NewId() };
        HandoutJobs[job.HandoutJobId] = job;
        return Task.FromResult(job);
    }

    public Task<WorkerHandoutJob?> GetHandoutJobAsync(string jobId, CancellationToken cancellationToken)
    {
        Record($"get-handout:{jobId}");
        return Task.FromResult(HandoutJobs.GetValueOrDefault(jobId));
    }

    public async Task DownloadHandoutFileAsync(string jobId, Stream destination, CancellationToken cancellationToken)
    {
        Record($"download-handout:{jobId}");
        if (DownloadFailure is { } failure)
        {
            throw failure;
        }

        await destination.WriteAsync(Pdf, cancellationToken);
    }

    public Task DeleteHandoutJobAsync(string jobId, CancellationToken cancellationToken) => Delete($"handout:{jobId}", () => HandoutJobs.TryRemove(jobId, out _));

    public Task<WorkerInsightsHealth?> GetInsightsHealthAsync(CancellationToken cancellationToken)
    {
        Record("health");
        return HealthUnavailable ? throw new WorkerUnavailableException("The processing service cannot be reached.") : Task.FromResult(Health);
    }

    /// <summary>Completes a quality job with the measures given (default: the contract example's result).</summary>
    public void CompleteQuality(string jobId, WorkerQualityResult? result = null) =>
        QualityJobs[jobId] = QualityJobs[jobId] with { Status = WorkerRenderJobState.Completed, Progress = 1, Result = result ?? Example<WorkerQualityJob>("quality-completed-job").Result };

    /// <summary>Moves a quality job to the given state.</summary>
    public void SetQuality(string jobId, WorkerRenderJobState status, double progress = 0, string? error = null) =>
        QualityJobs[jobId] = QualityJobs[jobId] with { Status = status, Progress = progress, Error = error };

    /// <summary>Completes a handout job: the size and hash match <see cref="Pdf"/> unless <paramref name="sha256"/> is given.</summary>
    public void CompleteHandout(string jobId, WorkerHandoutResult? result = null, string? sha256 = null)
    {
        var example = result ?? Example<WorkerHandoutJob>("handout-completed-job").Result!;
        var hash = sha256 ?? Convert.ToHexStringLower(SHA256.HashData(Pdf));
        HandoutJobs[jobId] = HandoutJobs[jobId] with { Status = WorkerRenderJobState.Completed, Progress = 1, Result = example with { SizeBytes = Pdf.Length, Sha256 = hash } };
    }

    /// <summary>Moves a handout job to the given state.</summary>
    public void SetHandout(string jobId, WorkerRenderJobState status, double progress = 0, string? error = null) =>
        HandoutJobs[jobId] = HandoutJobs[jobId] with { Status = status, Progress = progress, Error = error };

    private static JsonSerializerOptions CreateJson() => WorkerInsightsJson.AddTo(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private void Submit(string call)
    {
        Record(call);
        if (SubmitFailure is { } failure)
        {
            throw failure;
        }
    }

    private void Record(string call)
    {
        Calls.Enqueue(call);
        if (Unavailable)
        {
            throw new WorkerUnavailableException("The processing service cannot be reached.");
        }
    }

    private Task Delete(string what, Func<bool> remove)
    {
        Record($"delete-{what}");
        remove();
        Deleted.Add(what);
        return Task.CompletedTask;
    }

    private string NewId() => Interlocked.Increment(ref _next).ToString("x32", CultureInfo.InvariantCulture);
}
