using System.Net;

namespace StemStudio.Api.Worker;

/// <summary>Transcription job calls of the worker API 1.6.0 (feature 008).</summary>
public sealed partial class WorkerClient
{
    private static readonly string[] ScoreFileRoles = ["pdf", "musicxml", "midi"];

    /// <inheritdoc />
    public async Task<WorkerTranscriptionJob> CreateTranscriptionJobAsync(WorkerTranscriptionJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/transcription-jobs") { Content = JsonContent.Create(request, options: Json) };
        return await SendForAsync<WorkerTranscriptionJob>(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerTranscriptionJob?> GetTranscriptionJobAsync(string transcriptionJobId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, TranscriptionJobPath(transcriptionJobId));
        using var response = await SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await ReadAsync<WorkerTranscriptionJob>(message, response, cancellationToken);
    }

    /// <inheritdoc />
    public Task DownloadTranscriptionFileAsync(string transcriptionJobId, string role, Stream destination, CancellationToken cancellationToken)
    {
        var path = TranscriptionJobPath(transcriptionJobId);
        return ScoreFileRoles.Contains(role, StringComparer.Ordinal)
            ? DownloadAsync($"{path}/files/{role}", destination, cancellationToken)
            : throw new ArgumentException($"'{role}' is not a score file role.", nameof(role));
    }

    /// <inheritdoc />
    public Task DeleteTranscriptionJobAsync(string transcriptionJobId, CancellationToken cancellationToken) =>
        DeleteToleratingMissingAsync(TranscriptionJobPath(transcriptionJobId), cancellationToken);

    private static string TranscriptionJobPath(string transcriptionJobId) => $"v1/transcription-jobs/{ValidJobId(transcriptionJobId)}";
}
