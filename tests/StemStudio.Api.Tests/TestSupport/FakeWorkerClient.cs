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

    public List<string> DetailCreates { get; } = [];

    public List<string> DetailCancels { get; } = [];

    public List<string> DetailStemDownloads { get; } = [];

    public Func<string, Task<WorkerDetailJob>>? OnCreateDetail { get; set; }

    public Func<string, Task<WorkerDetailJob?>>? OnGetDetailJob { get; set; }

    public Func<string, Task<WorkerDetailJob>>? OnCancelDetail { get; set; }

    public Func<string, Task<WorkerDetailResult>>? OnGetDetailResult { get; set; }

    public Func<WorkerDetailStemRef, Stream, Task>? OnDownloadDetailStem { get; set; }

    public Func<Task<WorkerHealth>>? OnGetHealth { get; set; }

    public int HealthCalls { get; private set; }

    public List<WorkerMetadataJobRequest> MetadataCreates { get; } = [];

    public List<WorkerArtworkRef> ArtworkDownloads { get; } = [];

    public List<(string FileName, string? ContentType, byte[] Bytes)> ArtworkUploads { get; } = [];

    public Func<WorkerMetadataJobRequest, Task<WorkerMetadataJob>>? OnCreateMetadata { get; set; }

    public Func<string, Task<WorkerMetadataJob?>>? OnGetMetadataJob { get; set; }

    public Func<string, Task<WorkerMetadataResult>>? OnGetMetadataResult { get; set; }

    public Func<WorkerArtworkRef, Task<byte[]?>>? OnGetArtwork { get; set; }

    public Func<WorkerArtworkFile, Task<WorkerArtworkUpload>>? OnNormalizeArtwork { get; set; }

    public static WorkerMetadataJob MetadataJob(string metadataJobId, WorkerMetadataJobState status, string? error = null) => new()
    {
        MetadataJobId = metadataJobId,
        Status = status,
        Stage = status.ToString(),
        Error = error,
    };

    /// <summary>Deterministic fake JPEG bytes of a picture variant: the variant name and the reference id.</summary>
    public static byte[] ArtworkBytes(WorkerArtworkRef artwork) => System.Text.Encoding.UTF8.GetBytes($"jpeg:{artwork.Variant}:{artwork.Id}");

    public static WorkerHealth Health(WorkerDeviceInfo deviceInfo) => new()
    {
        Status = "ok",
        Separator = "fake",
        Device = deviceInfo.Kind == ProcessingDeviceKind.Gpu ? "auto" : "cpu",
        DeviceInfo = deviceInfo,
        ContractVersion = "1.2.0",
    };

    public static WorkerDetailJob DetailJob(string detailJobId, WorkerDetailJobState status, int progress = 0, string? parent = null) => new()
    {
        DetailJobId = detailJobId,
        StandardJobId = "0123456789abcdef0123456789abcdef",
        Status = status,
        Stage = status.ToString(),
        CurrentParent = parent,
        Progress = progress,
        Error = status == WorkerDetailJobState.Failed ? "Not enough memory for detail separation." : null,
    };

    /// <summary>Six-stem result: vocals (lead, backing, rest) and drums (kick, snare, rest) split, the others not.</summary>
    public static WorkerDetailResult DetailResult(string detailJobId) => new()
    {
        DetailJobId = detailJobId,
        Parents =
        [
            DetailSplit(detailJobId, "vocals", "lead", "backing", "rest"),
            DetailSplit(detailJobId, "drums", "kick", "snare", "rest"),
            new WorkerParentOutcome("bass", WorkerParentOutcomeKind.NotSupported, []),
            new WorkerParentOutcome("guitar", WorkerParentOutcomeKind.NothingFurther, []),
            new WorkerParentOutcome("piano", WorkerParentOutcomeKind.SkippedSilent, []),
            new WorkerParentOutcome("other", WorkerParentOutcomeKind.NotReliable, []),
        ],
    };

    public static WorkerParentOutcome DetailSplit(string detailJobId, string parent, params string[] names) =>
        new(parent, WorkerParentOutcomeKind.Split, names.Select(name => new WorkerDetailStemInfo(name, $"/v1/detail-jobs/{detailJobId}/stems/{parent}/{name}", 100)).ToList());

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

    public Task<WorkerDetailJob> CreateDetailJobAsync(string standardJobId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            DetailCreates.Add(standardJobId);
        }

        return OnCreateDetail?.Invoke(standardJobId) ?? Task.FromResult(DetailJob(NextJobId(), WorkerDetailJobState.Queued));
    }

    public Task<WorkerDetailJob?> GetDetailJobAsync(string detailJobId, CancellationToken cancellationToken) =>
        OnGetDetailJob?.Invoke(detailJobId) ?? Task.FromResult<WorkerDetailJob?>(DetailJob(detailJobId, WorkerDetailJobState.Queued));

    public Task<WorkerDetailJob> CancelDetailJobAsync(string detailJobId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            DetailCancels.Add(detailJobId);
        }

        return OnCancelDetail?.Invoke(detailJobId) ?? Task.FromResult(DetailJob(detailJobId, WorkerDetailJobState.Cancelled));
    }

    public Task<WorkerDetailResult> GetDetailResultAsync(string detailJobId, CancellationToken cancellationToken) =>
        OnGetDetailResult?.Invoke(detailJobId) ?? Task.FromResult(DetailResult(detailJobId));

    public async Task DownloadDetailStemAsync(WorkerDetailStemRef stem, Stream destination, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            DetailStemDownloads.Add($"{stem.Parent}/{stem.Name}");
        }

        if (OnDownloadDetailStem is not null)
        {
            await OnDownloadDetailStem(stem, destination);
            return;
        }

        await destination.WriteAsync(System.Text.Encoding.UTF8.GetBytes($"fLaC:{stem.Parent}/{stem.Name}"), cancellationToken);
    }

    public Task<WorkerHealth> GetHealthAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            HealthCalls++;
        }

        return OnGetHealth?.Invoke() ?? Task.FromResult(Health(new WorkerDeviceInfo { Kind = ProcessingDeviceKind.Cpu }));
    }

    public Task<WorkerMetadataJob> CreateMetadataJobAsync(WorkerMetadataJobRequest request, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            MetadataCreates.Add(request);
        }

        return OnCreateMetadata?.Invoke(request) ?? Task.FromResult(MetadataJob(NextJobId(), WorkerMetadataJobState.Queued));
    }

    public Task<WorkerMetadataJob?> GetMetadataJobAsync(string metadataJobId, CancellationToken cancellationToken) =>
        OnGetMetadataJob?.Invoke(metadataJobId) ?? Task.FromResult<WorkerMetadataJob?>(MetadataJob(metadataJobId, WorkerMetadataJobState.Running));

    public Task<WorkerMetadataResult> GetMetadataResultAsync(string metadataJobId, CancellationToken cancellationToken) =>
        OnGetMetadataResult?.Invoke(metadataJobId) ?? Task.FromResult(new WorkerMetadataResult { Outcome = WorkerMetadataOutcome.NotFound });

    public Task<byte[]?> GetArtworkAsync(WorkerArtworkRef artwork, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            ArtworkDownloads.Add(artwork);
        }

        return OnGetArtwork?.Invoke(artwork) ?? Task.FromResult<byte[]?>(ArtworkBytes(artwork));
    }

    public async Task<WorkerArtworkUpload> NormalizeArtworkAsync(WorkerArtworkFile file, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await file.Content.CopyToAsync(copy, cancellationToken);
        lock (_lock)
        {
            ArtworkUploads.Add((file.FileName, file.ContentType, copy.ToArray()));
        }

        return await (OnNormalizeArtwork?.Invoke(file)
            ?? Task.FromResult(new WorkerArtworkUpload("8e4f2a6c0b1d4e3f9a7c5b3d1e0f2a4c", new WorkerArtworkInfo(WorkerArtworkSource.User, new string('e', 64), 100, 10))));
    }
}
