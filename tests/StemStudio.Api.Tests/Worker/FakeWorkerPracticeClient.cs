using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;

namespace StemStudio.Api.Tests.Worker;

/// <summary>
/// In-memory worker practice lane (worker 1.8.0) for API tests, consumer-driven from the worker examples
/// (<c>section-*</c>, <c>score-view-*</c>, <c>setlist-document-*</c>): jobs start queued and the test completes or fails them.
/// </summary>
internal sealed class FakeWorkerPracticeClient : IWorkerPracticeClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private int _next;

    /// <summary>When true every call throws <see cref="WorkerUnavailableException"/> (worker down).</summary>
    public bool Unavailable { get; set; }

    /// <summary>When set, every job submission (<c>Create*</c>) throws it (e.g. a <see cref="WorkerRequestException"/> 404).</summary>
    public Exception? SubmitFailure { get; set; }

    /// <summary>When set, every file download (pages, PDF) throws it (e.g. a <see cref="WorkerResponseTooLargeException"/>).</summary>
    public Exception? DownloadFailure { get; set; }

    /// <summary>Calls in order (<c>create-section</c>, <c>get-section:&lt;id&gt;</c>, ...).</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    public ConcurrentQueue<WorkerSectionJobRequest> SectionRequests { get; } = new();

    public ConcurrentQueue<WorkerScoreViewJobRequest> ScoreViewRequests { get; } = new();

    public ConcurrentQueue<WorkerSetlistDocumentRequest> DocumentRequests { get; } = new();

    public ConcurrentDictionary<string, WorkerSectionJob> SectionJobs { get; } = new();

    public ConcurrentDictionary<string, WorkerScoreViewJob> ScoreViewJobs { get; } = new();

    public ConcurrentDictionary<string, WorkerSetlistDocumentJob> DocumentJobs { get; } = new();

    public ConcurrentBag<string> Deleted { get; } = [];

    /// <summary>SVG of every page of a score view.</summary>
    public string PageSvg { get; set; } = "<svg xmlns=\"http://www.w3.org/2000/svg\"><style>@font-face{font-family:Leipzig}</style></svg>";

    /// <summary>The PDF bytes of a setlist document.</summary>
    public byte[] Pdf { get; set; } = Encoding.ASCII.GetBytes("%PDF-1.7 fake");

    /// <summary>Setlist documents complete after this many reads (0 = already on the first read; -1 = never).</summary>
    public int DocumentReadsUntilDone { get; set; }

    public static T Example<T>(string name) => JsonSerializer.Deserialize<T>(WorkerExamples.Read(name), Json)!;

    public Task<WorkerSectionJob> CreateSectionJobAsync(WorkerSectionJobRequest request, CancellationToken cancellationToken)
    {
        Submit("create-section");
        SectionRequests.Enqueue(request);
        var job = Example<WorkerSectionJob>("section-queued-job") with { JobId = NewId() };
        SectionJobs[job.JobId] = job;
        return Task.FromResult(job);
    }

    public Task<WorkerSectionJob?> GetSectionJobAsync(string jobId, CancellationToken cancellationToken)
    {
        Record($"get-section:{jobId}");
        return Task.FromResult(SectionJobs.GetValueOrDefault(jobId));
    }

    public Task DeleteSectionJobAsync(string jobId, CancellationToken cancellationToken) => Delete(jobId, SectionJobs);

    public Task<WorkerScoreViewJob> CreateScoreViewJobAsync(WorkerScoreViewJobRequest request, CancellationToken cancellationToken)
    {
        Submit("create-score-view");
        ScoreViewRequests.Enqueue(request);
        var job = new WorkerScoreViewJob { JobId = NewId(), Status = WorkerPracticeJobState.Queued };
        ScoreViewJobs[job.JobId] = job;
        return Task.FromResult(job);
    }

    public Task<WorkerScoreViewJob?> GetScoreViewJobAsync(string jobId, CancellationToken cancellationToken)
    {
        Record($"get-score-view:{jobId}");
        return Task.FromResult(ScoreViewJobs.GetValueOrDefault(jobId));
    }

    public async Task DownloadScoreViewPageAsync(string jobId, int page, Stream destination, CancellationToken cancellationToken)
    {
        Download($"page:{jobId}:{page}");
        await destination.WriteAsync(Encoding.UTF8.GetBytes(PageSvg), cancellationToken);
    }

    public Task DeleteScoreViewJobAsync(string jobId, CancellationToken cancellationToken) => Delete(jobId, ScoreViewJobs);

    public Task<WorkerSetlistDocumentJob> CreateSetlistDocumentJobAsync(WorkerSetlistDocumentRequest request, CancellationToken cancellationToken)
    {
        Submit("create-document");
        DocumentRequests.Enqueue(request);
        var job = new WorkerSetlistDocumentJob { JobId = NewId(), Status = WorkerPracticeJobState.Queued };
        DocumentJobs[job.JobId] = job;
        return Task.FromResult(job);
    }

    public Task<WorkerSetlistDocumentJob?> GetSetlistDocumentJobAsync(string jobId, CancellationToken cancellationToken)
    {
        Record($"get-document:{jobId}");
        if (DocumentJobs.TryGetValue(jobId, out var job) && job.Status == WorkerPracticeJobState.Queued && DocumentReadsUntilDone >= 0 && DocumentReadsUntilDone-- == 0)
        {
            DocumentJobs[jobId] = job = Example<WorkerSetlistDocumentJob>("setlist-document-completed-job") with { JobId = jobId };
        }

        return Task.FromResult(DocumentJobs.GetValueOrDefault(jobId));
    }

    public async Task DownloadSetlistDocumentAsync(string jobId, Stream destination, CancellationToken cancellationToken)
    {
        Download($"pdf:{jobId}");
        await destination.WriteAsync(Pdf, cancellationToken);
    }

    public Task DeleteSetlistDocumentJobAsync(string jobId, CancellationToken cancellationToken) => Delete(jobId, DocumentJobs);

    /// <summary>Completes a section job with the <c>section-completed-job</c> example (or the given result).</summary>
    public void CompleteSection(string jobId, WorkerSectionResult? result = null)
    {
        var done = Example<WorkerSectionJob>("section-completed-job") with { JobId = jobId };
        SectionJobs[jobId] = result is null ? done : done with { Result = result };
    }

    /// <summary>Fails a section job with the <c>section-failed-job</c> example.</summary>
    public void FailSection(string jobId) => SectionJobs[jobId] = Example<WorkerSectionJob>("section-failed-job") with { JobId = jobId };

    /// <summary>Completes a score view job with the <c>score-view-completed-job</c> example (or the given result).</summary>
    public void CompleteScoreView(string jobId, WorkerScoreViewResult? result = null)
    {
        var done = Example<WorkerScoreViewJob>("score-view-completed-job") with { JobId = jobId };
        ScoreViewJobs[jobId] = result is null ? done : done with { Result = result };
    }

    /// <summary>Sets the state of a job (running, failed with a text, ...).</summary>
    public void SetScoreView(string jobId, WorkerPracticeJobState state, string? error = null) =>
        ScoreViewJobs[jobId] = ScoreViewJobs[jobId] with { Status = state, Error = error };

    /// <summary>Sets the state of a section job.</summary>
    public void SetSection(string jobId, WorkerPracticeJobState state) => SectionJobs[jobId] = SectionJobs[jobId] with { Status = state };

    /// <summary>Sets the state of a document job.</summary>
    public void SetDocument(string jobId, WorkerPracticeJobState state, string? error = null) =>
        DocumentJobs[jobId] = DocumentJobs[jobId] with { Status = state, Error = error };

    private string NewId() => Interlocked.Increment(ref _next).ToString("x32", System.Globalization.CultureInfo.InvariantCulture);

    private Task Delete<T>(string jobId, ConcurrentDictionary<string, T> jobs)
    {
        Record($"delete:{jobId}");
        jobs.TryRemove(jobId, out _);
        Deleted.Add(jobId);
        return Task.CompletedTask;
    }

    private void Submit(string call)
    {
        Record(call);
        if (SubmitFailure is { } failure)
        {
            throw failure;
        }
    }

    private void Download(string call)
    {
        Record(call);
        if (DownloadFailure is { } failure)
        {
            throw failure;
        }
    }

    private void Record(string call)
    {
        if (Unavailable)
        {
            throw new WorkerUnavailableException("The processing service cannot be reached.");
        }

        Calls.Enqueue(call);
    }
}
