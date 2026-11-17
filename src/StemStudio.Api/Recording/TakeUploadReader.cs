using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using StemStudio.Api.Importing;
using StemStudio.Contracts;
using StemStudio.Domain.Recording;

namespace StemStudio.Api.Recording;

/// <summary>A read take upload: the metadata, the validated WAV header and the spooled audio data.</summary>
/// <typeparam name="T">The metadata type.</typeparam>
/// <param name="Metadata">The metadata part.</param>
/// <param name="Header">The WAV format.</param>
/// <param name="SpoolPath">The spooled data bytes (deleted by the caller).</param>
public sealed record TakeUpload<T>(T Metadata, TakeWavHeader Header, string SpoolPath);

/// <summary>
/// Streams the multipart body of the two take upload endpoints (parts <c>audio</c> and <c>metadata</c> in any order): the
/// endpoint's own body limit (<see cref="RecordingOptions.MaxUploadBytes"/>), the WAV header checked before any data is
/// stored, the audio data spooled to disk without buffering, and the metadata JSON read with a 64 KiB cap. Hostile input
/// ends as 400, 413 or 415 and leaves no file behind.
/// </summary>
/// <param name="storage">Take files.</param>
/// <param name="options">Recording options.</param>
/// <param name="json">The API's JSON options.</param>
public sealed class TakeUploadReader(TakeFiles storage, IOptions<RecordingOptions> options, IOptions<JsonOptions> json)
{
    /// <summary>Largest metadata part.</summary>
    public const int MaxMetadataBytes = 64 * 1024;

    private const int BufferBytes = 64 * 1024;

    private long MaxDataBytes => options.Value.MaxUploadBytes;

    private long MaxBodyBytes => MaxDataBytes + UploadLimits.MultipartOverheadBytes;

    /// <summary>Reads the upload.</summary>
    /// <typeparam name="T">The metadata type.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="assetId">The asset folder the spool is written to.</param>
    /// <param name="admit">Checks the WAV header before the data is stored (e.g. free disk space); null to continue.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The upload or the problem.</returns>
    public async Task<(TakeUpload<T>? Upload, IResult? Problem)> ReadAsync<T>(HttpRequest request, Guid assetId, Func<TakeWavHeader, IResult?> admit, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(admit);
        if (OpenBody(request) is { } refused)
        {
            return (null, refused);
        }

        var boundary = UploadRequestReader.Boundary(request.ContentType);
        if (boundary is null)
        {
            return (null, ApiProblems.Validation("audio", "Send the take as multipart/form-data with the parts 'audio' and 'metadata'."));
        }

        var state = new ReadState<T>(storage.NewSpoolPath(assetId), admit);
        var problem = await ReadGuardedAsync(new MultipartReader(boundary, request.Body), state, cancellationToken);
        if (problem is null && state.Complete)
        {
            return (new TakeUpload<T>(state.Metadata!, state.Header!, state.SpoolPath), null);
        }

        TakeFiles.TryDelete(state.SpoolPath);
        return (null, problem ?? ApiProblems.Validation(state.Header is null ? "audio" : "metadata", "Send both parts: 'audio' (the WAV) and 'metadata' (JSON)."));
    }

    /// <summary>Raises the body limit of this request to the take limit; 413 when the declared length is already larger.</summary>
    private IResult? OpenBody(HttpRequest request)
    {
        if (request.ContentLength > MaxBodyBytes)
        {
            return RecordingProblems.TooLarge();
        }

        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = MaxBodyBytes;
        }

        return null;
    }

    private async Task<IResult?> ReadGuardedAsync<T>(MultipartReader reader, ReadState<T> state, CancellationToken cancellationToken)
        where T : class
    {
        reader.BodyLengthLimit = MaxBodyBytes;
        try
        {
            return await ReadSectionsAsync(reader, state, cancellationToken);
        }
        catch (TakeWavException refused)
        {
            return RecordingProblems.Wav(refused);
        }
        catch (InvalidDataException)
        {
            return RecordingProblems.TooLarge();
        }
        catch (BadHttpRequestException exception)
        {
            return exception.StatusCode == StatusCodes.Status413PayloadTooLarge ? RecordingProblems.TooLarge() : RecordingProblems.Invalid("The upload was incomplete. Try again.");
        }
    }

    private async Task<IResult?> ReadSectionsAsync<T>(MultipartReader reader, ReadState<T> state, CancellationToken cancellationToken)
        where T : class
    {
        while (await reader.ReadNextSectionAsync(cancellationToken) is { } section)
        {
            var problem = PartName(section) switch
            {
                ApiRoutes.RecordingTakeAudioPart => await AudioAsync(section.Body, state, cancellationToken),
                ApiRoutes.RecordingTakeMetadataPart => await MetadataAsync(section.Body, state, cancellationToken),
                _ => null,
            };
            if (problem is not null)
            {
                return problem;
            }
        }

        return null;
    }

    private async Task<IResult?> AudioAsync<T>(Stream body, ReadState<T> state, CancellationToken cancellationToken)
        where T : class
    {
        if (state.Header is not null)
        {
            return ApiProblems.Validation("audio", "Send only one 'audio' part.");
        }

        var header = await TakeWavReader.ReadHeaderAsync(body, MaxDataBytes, cancellationToken);
        if (state.Admit(header) is { } refused)
        {
            return refused;
        }

        await SpoolAsync(body, header.DataBytes, state.SpoolPath, cancellationToken);
        state.Header = header;
        return null;
    }

    private static async Task SpoolAsync(Stream body, long count, string path, CancellationToken cancellationToken)
    {
        await using var spool = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferBytes, useAsync: true);
        var buffer = new byte[BufferBytes];
        for (var left = count; left > 0;)
        {
            var read = await body.ReadAsync(buffer.AsMemory(0, (int)Math.Min(left, buffer.Length)), cancellationToken);
            if (read == 0)
            {
                throw new TakeWavException("The file ends before its audio data does.");
            }

            await spool.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            left -= read;
        }
    }

    private async Task<IResult?> MetadataAsync<T>(Stream body, ReadState<T> state, CancellationToken cancellationToken)
        where T : class
    {
        if (state.Metadata is not null)
        {
            return ApiProblems.Validation("metadata", "Send only one 'metadata' part.");
        }

        var bytes = await ReadCappedAsync(body, cancellationToken);
        try
        {
            state.Metadata = bytes is null ? null : JsonSerializer.Deserialize<T>(bytes, json.Value.SerializerOptions);
        }
        catch (JsonException)
        {
            state.Metadata = null;
        }

        return state.Metadata is null ? ApiProblems.Validation("metadata", "The metadata part is not valid JSON as described in the API contract.") : null;
    }

    private static async Task<byte[]?> ReadCappedAsync(Stream body, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (copy.Length + read > MaxMetadataBytes)
            {
                return null;
            }

            copy.Write(buffer, 0, read);
        }

        return copy.ToArray();
    }

    private static string? PartName(MultipartSection section) =>
        ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) ? HeaderUtilities.RemoveQuotes(disposition.Name).Value : null;

    private sealed class ReadState<T>(string spoolPath, Func<TakeWavHeader, IResult?> admit)
        where T : class
    {
        public string SpoolPath { get; } = spoolPath;

        public Func<TakeWavHeader, IResult?> Admit { get; } = admit;

        public TakeWavHeader? Header { get; set; }

        public T? Metadata { get; set; }

        public bool Complete => Header is not null && Metadata is not null;
    }
}
