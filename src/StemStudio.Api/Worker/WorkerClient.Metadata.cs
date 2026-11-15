using System.Net;
using System.Net.Http.Headers;

namespace StemStudio.Api.Worker;

/// <summary>Artwork and metadata job calls of the worker API 1.3.0 (feature 005).</summary>
public sealed partial class WorkerClient
{
    /// <summary>Largest picture body accepted from the worker (the cover is at most 200 KB; 1 MiB leaves room).</summary>
    public const int MaxArtworkBytes = 1024 * 1024;

    /// <inheritdoc />
    public async Task<WorkerMetadataJob> CreateMetadataJobAsync(WorkerMetadataJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/metadata-jobs") { Content = JsonContent.Create(request, options: Json) };
        return await SendForAsync<WorkerMetadataJob>(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerMetadataJob?> GetMetadataJobAsync(string metadataJobId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, MetadataJobPath(metadataJobId));
        using var response = await SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await ReadAsync<WorkerMetadataJob>(message, response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerMetadataResult> GetMetadataResultAsync(string metadataJobId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{MetadataJobPath(metadataJobId)}/result");
        return await SendForAsync<WorkerMetadataResult>(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetArtworkAsync(WorkerArtworkRef artwork, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, ArtworkPath(artwork));
        using var response = await SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(message, response, cancellationToken);
        return await ReadLimitedAsync(response.Content, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerArtworkUpload> NormalizeArtworkAsync(WorkerArtworkFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        var part = new StreamContent(file.Content);
        part.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
        using var content = new MultipartFormDataContent { { part, "file", file.FileName } };
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/artwork") { Content = content };
        return await SendForAsync<WorkerArtworkUpload>(message, cancellationToken);
    }

    private static string MetadataJobPath(string metadataJobId) => $"v1/metadata-jobs/{ValidJobId(metadataJobId)}";

    private static string ArtworkPath(WorkerArtworkRef artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        var variant = artwork.Variant is "cover" or "thumb"
            ? artwork.Variant
            : throw new ArgumentException($"'{artwork.Variant}' is not an artwork variant.", nameof(artwork));
        return artwork.Owner switch
        {
            WorkerArtworkOwner.Job => $"{JobPath(artwork.Id)}/artwork/{variant}",
            WorkerArtworkOwner.MetadataJob => $"{MetadataJobPath(artwork.Id)}/artwork/{variant}",
            WorkerArtworkOwner.Upload => $"v1/artwork/{ValidJobId(artwork.Id)}/{variant}",
            _ => throw new ArgumentException($"'{artwork.Owner}' is not an artwork owner.", nameof(artwork)),
        };
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxArtworkBytes)
            {
                throw new WorkerUnavailableException("The processing service sent an oversized picture.");
            }
        }

        return buffer.ToArray();
    }
}
