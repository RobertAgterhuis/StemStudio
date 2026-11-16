namespace StemStudio.Api.Generator;

/// <summary>Songs, voice conversions and voice-sample checks (generator 1.1.0).</summary>
public partial class GeneratorClient
{
    /// <inheritdoc />
    public async Task<GeneratorJob> CreateSongAsync(GeneratorSongParams song, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(song);
        var request = new HttpRequestMessage(HttpMethod.Post, "v1/generations") { Content = ParamsPart(new { kind = "song", song }) };
        return (await ReadAsync<GeneratorJob>(request, false, cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<GeneratorJob> CreateVoiceConversionAsync(GeneratorVoiceConversionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var content = new MultipartFormDataContent
        {
            { ParamsPart(new { kind = "voiceConversion", voiceConversion = request.Params }), "params" },
            { FilePart(request.TargetPath), "target", Path.GetFileName(request.TargetPath) },
            { FilePart(request.PromptPath), "prompt", Path.GetFileName(request.PromptPath) },
        };
        foreach (var path in request.AccompanimentPaths)
        {
            content.Add(FilePart(path), "accompaniment", Path.GetFileName(path));
        }

        return (await ReadAsync<GeneratorJob>(new HttpRequestMessage(HttpMethod.Post, "v1/generations") { Content = content }, false, cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<GeneratorSampleCheck> CheckVoiceSampleAsync(Stream sample, string fileName, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent { { new StreamContent(sample), "sample", fileName } };
        return (await ReadAsync<GeneratorSampleCheck>(new HttpRequestMessage(HttpMethod.Post, "v1/voice-samples/check") { Content = content }, false, cancellationToken))!;
    }

    /// <inheritdoc />
    public Task DownloadCheckedSampleAsync(string checkId, Stream destination, CancellationToken cancellationToken) =>
        CopyAsync($"v1/voice-samples/{ValidId(checkId)}/audio", destination, cancellationToken);

    /// <inheritdoc />
    public Task DownloadTakeVocalAsync(string generationId, int index, Stream destination, CancellationToken cancellationToken) =>
        CopyAsync($"{JobPath(generationId)}/takes/{TakeIndex(index)}/vocal", destination, cancellationToken);

    private async Task CopyAsync(string path, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await ExchangeAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await ThrowOnFailureAsync(request, response, cancellationToken);
        await response.Content.CopyToAsync(destination, cancellationToken);
    }

    private static string ValidId(string id) =>
        JobIdPattern().IsMatch(id ?? string.Empty) ? id! : throw new ArgumentException($"'{id}' is not a check id.", nameof(id));
}
