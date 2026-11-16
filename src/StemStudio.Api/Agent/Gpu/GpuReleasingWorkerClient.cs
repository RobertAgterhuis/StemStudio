using StemStudio.Api.Worker;

namespace StemStudio.Api.Agent.Gpu;

/// <summary>
/// Decorates the worker client (feature 011, T053/T078): every call that submits a GPU-heavy worker job (standard and detail
/// separation, lyrics alignment, transcription) first passes the <see cref="GpuJobGate"/>; everything else is forwarded as is.
/// One decorator covers every endpoint that submits such a job (imports, uploads, track retries, detail, lyrics alignment,
/// scores, take imports and the take separation of a voice conversion).
/// </summary>
/// <param name="inner">The real worker client.</param>
/// <param name="gate">The GPU release gate.</param>
public sealed class GpuReleasingWorkerClient(IWorkerClient inner, GpuJobGate gate) : IWorkerClient
{
    /// <inheritdoc />
    public async Task<WorkerJob> CreateUrlJobAsync(WorkerUrlJobRequest request, CancellationToken cancellationToken)
    {
        await gate.BeforeGpuJobAsync(cancellationToken);
        return await inner.CreateUrlJobAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerJob> CreateUploadJobAsync(WorkerUpload upload, CancellationToken cancellationToken)
    {
        await gate.BeforeGpuJobAsync(cancellationToken);
        return await inner.CreateUploadJobAsync(upload, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerDetailJob> CreateDetailJobAsync(string standardJobId, CancellationToken cancellationToken)
    {
        await gate.BeforeGpuJobAsync(cancellationToken);
        return await inner.CreateDetailJobAsync(standardJobId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerLyricsJob> CreateLyricsAlignJobAsync(WorkerLyricsAlignRequest request, CancellationToken cancellationToken)
    {
        await gate.BeforeGpuJobAsync(cancellationToken);
        return await inner.CreateLyricsAlignJobAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerTranscriptionJob> CreateTranscriptionJobAsync(WorkerTranscriptionJobRequest request, CancellationToken cancellationToken)
    {
        await gate.BeforeGpuJobAsync(cancellationToken);
        return await inner.CreateTranscriptionJobAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<WorkerJob?> GetJobAsync(string jobId, CancellationToken cancellationToken) => inner.GetJobAsync(jobId, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerJobResult> GetResultAsync(string jobId, CancellationToken cancellationToken) => inner.GetResultAsync(jobId, cancellationToken);

    /// <inheritdoc />
    public Task DownloadStemAsync(string jobId, string stem, Stream destination, CancellationToken cancellationToken) =>
        inner.DownloadStemAsync(jobId, stem, destination, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerDetailJob?> GetDetailJobAsync(string detailJobId, CancellationToken cancellationToken) => inner.GetDetailJobAsync(detailJobId, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerDetailJob> CancelDetailJobAsync(string detailJobId, CancellationToken cancellationToken) => inner.CancelDetailJobAsync(detailJobId, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerDetailResult> GetDetailResultAsync(string detailJobId, CancellationToken cancellationToken) => inner.GetDetailResultAsync(detailJobId, cancellationToken);

    /// <inheritdoc />
    public Task DownloadDetailStemAsync(WorkerDetailStemRef stem, Stream destination, CancellationToken cancellationToken) =>
        inner.DownloadDetailStemAsync(stem, destination, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerHealth> GetHealthAsync(CancellationToken cancellationToken) => inner.GetHealthAsync(cancellationToken);

    /// <inheritdoc />
    public Task<WorkerMetadataJob> CreateMetadataJobAsync(WorkerMetadataJobRequest request, CancellationToken cancellationToken) =>
        inner.CreateMetadataJobAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerMetadataJob?> GetMetadataJobAsync(string metadataJobId, CancellationToken cancellationToken) => inner.GetMetadataJobAsync(metadataJobId, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerMetadataResult> GetMetadataResultAsync(string metadataJobId, CancellationToken cancellationToken) =>
        inner.GetMetadataResultAsync(metadataJobId, cancellationToken);

    /// <inheritdoc />
    public Task<byte[]?> GetArtworkAsync(WorkerArtworkRef artwork, CancellationToken cancellationToken) => inner.GetArtworkAsync(artwork, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerArtworkUpload> NormalizeArtworkAsync(WorkerArtworkFile file, CancellationToken cancellationToken) => inner.NormalizeArtworkAsync(file, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerRenderJob> CreateRenderJobAsync(WorkerRenderJobRequest request, CancellationToken cancellationToken) => inner.CreateRenderJobAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerRenderJob?> GetRenderJobAsync(string renderJobId, CancellationToken cancellationToken) => inner.GetRenderJobAsync(renderJobId, cancellationToken);

    /// <inheritdoc />
    public Task DownloadRenderFileAsync(string renderJobId, Stream destination, CancellationToken cancellationToken) =>
        inner.DownloadRenderFileAsync(renderJobId, destination, cancellationToken);

    /// <inheritdoc />
    public Task DeleteRenderJobAsync(string renderJobId, CancellationToken cancellationToken) => inner.DeleteRenderJobAsync(renderJobId, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerLyricsJob> CreateLyricsLookupJobAsync(WorkerLyricsLookupRequest request, CancellationToken cancellationToken) =>
        inner.CreateLyricsLookupJobAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerLyricsJob?> GetLyricsJobAsync(string lyricsJobId, CancellationToken cancellationToken) => inner.GetLyricsJobAsync(lyricsJobId, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerLyricsResult> GetLyricsResultAsync(string lyricsJobId, CancellationToken cancellationToken) => inner.GetLyricsResultAsync(lyricsJobId, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerLyricsJob> CancelLyricsJobAsync(string lyricsJobId, CancellationToken cancellationToken) => inner.CancelLyricsJobAsync(lyricsJobId, cancellationToken);

    /// <inheritdoc />
    public Task DeleteLyricsJobAsync(string lyricsJobId, CancellationToken cancellationToken) => inner.DeleteLyricsJobAsync(lyricsJobId, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerTranscriptionJob?> GetTranscriptionJobAsync(string transcriptionJobId, CancellationToken cancellationToken) =>
        inner.GetTranscriptionJobAsync(transcriptionJobId, cancellationToken);

    /// <inheritdoc />
    public Task DownloadTranscriptionFileAsync(string transcriptionJobId, string role, Stream destination, CancellationToken cancellationToken) =>
        inner.DownloadTranscriptionFileAsync(transcriptionJobId, role, destination, cancellationToken);

    /// <inheritdoc />
    public Task DeleteTranscriptionJobAsync(string transcriptionJobId, CancellationToken cancellationToken) =>
        inner.DeleteTranscriptionJobAsync(transcriptionJobId, cancellationToken);
}
