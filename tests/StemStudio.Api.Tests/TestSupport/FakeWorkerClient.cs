using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>In-memory <see cref="IWorkerClient"/>; behaviour is configured per test through the delegates.</summary>
internal sealed class FakeWorkerClient : IWorkerClient
{
    private readonly Lock _lock = new();
    private int _jobCounter;

    public List<WorkerUrlJobRequest> UrlRequests { get; } = [];

    public List<(WorkerUpload Upload, byte[] Bytes)> Uploads { get; } = [];

    public List<string> StemDownloads { get; } = [];

    public Func<WorkerUrlJobRequest, Task<WorkerJob>>? OnCreateUrl { get; set; }

    public Func<WorkerUpload, Task<WorkerJob>>? OnCreateUpload { get; set; }

    public Func<string, Task<WorkerJob?>>? OnGetJob { get; set; }

    public Func<string, Task<WorkerJobResult>>? OnGetResult { get; set; }

    public Func<string, string, Stream, Task>? OnDownloadStem { get; set; }

    public int CreateCalls
    {
        get
        {
            lock (_lock)
            {
                return UrlRequests.Count + Uploads.Count;
            }
        }
    }

    public static WorkerJob Job(string jobId, WorkerJobState status, int progress = 0, string? stage = null) => new()
    {
        JobId = jobId,
        SourceKey = "youtube:dQw4w9WgXcQ",
        Status = status,
        Stage = stage ?? status.ToString(),
        Progress = progress,
    };

    public static WorkerJobResult Result(string jobId, string model = "htdemucs_6s", IReadOnlyList<string>? stems = null) => new()
    {
        JobId = jobId,
        Metadata = new WorkerSourceMetadata("Never Gonna Give You Up", "Rick Astley", "https://www.youtube.com/watch?v=dQw4w9WgXcQ"),
        DurationSeconds = 213.04,
        Model = model,
        Stems = (stems ?? StemCatalog.SixStem).Select(name => new WorkerStemInfo(name, $"/v1/jobs/{jobId}/stems/{name}", 100)).ToList(),
        BeatGrid = new WorkerBeatGrid { Bpm = 113.5, Beats = [0.52, 1.05, 1.58], Downbeats = [0.52], BeatsPerBar = 4, Confidence = 0.87, Reliable = true },
    };

    public string NextJobId()
    {
        lock (_lock)
        {
            return (++_jobCounter).ToString("x32", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public Task<WorkerJob> CreateUrlJobAsync(WorkerUrlJobRequest request, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            UrlRequests.Add(request);
        }

        return OnCreateUrl?.Invoke(request) ?? Task.FromResult(Job(NextJobId(), WorkerJobState.Queued));
    }

    public async Task<WorkerJob> CreateUploadJobAsync(WorkerUpload upload, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(upload.FilePath, cancellationToken);
        lock (_lock)
        {
            Uploads.Add((upload, bytes));
        }

        return await (OnCreateUpload?.Invoke(upload) ?? Task.FromResult(Job(NextJobId(), WorkerJobState.Queued)));
    }

    public Task<WorkerJob?> GetJobAsync(string jobId, CancellationToken cancellationToken) =>
        OnGetJob?.Invoke(jobId) ?? Task.FromResult<WorkerJob?>(Job(jobId, WorkerJobState.Queued));

    public Task<WorkerJobResult> GetResultAsync(string jobId, CancellationToken cancellationToken) =>
        OnGetResult?.Invoke(jobId) ?? Task.FromResult(Result(jobId));

    public async Task DownloadStemAsync(string jobId, string stem, Stream destination, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            StemDownloads.Add(stem);
        }

        if (OnDownloadStem is not null)
        {
            await OnDownloadStem(jobId, stem, destination);
            return;
        }

        await destination.WriteAsync(System.Text.Encoding.UTF8.GetBytes("fLaC:" + stem), cancellationToken);
    }
}
