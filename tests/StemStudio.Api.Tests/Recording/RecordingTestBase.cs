using System.Buffers.Binary;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Assets;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Recording;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Recording;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence;

namespace StemStudio.Api.Tests.Recording;

/// <summary>Hosts the API for the feature 015 tests: a completed 213.04 s track, a fake disk and take upload helpers.</summary>
public abstract class RecordingTestBase : IDisposable
{
    public const string RenderJobId = "a1b2c3d4e5f60718293a4b5c6d7e8f90";

    private protected RecordingTestBase(IReadOnlyDictionary<string, string?>? settings = null)
    {
        Factory = new ApiFactory
        {
            ConfigureServices = services => services.AddSingleton<IDiskSpace>(Disk),
            Settings = settings ?? new Dictionary<string, string?>(),
        };
        Client = Factory.CreateClient();
        Worker.OnCreateRender = _ => Task.FromResult(FakeWorkerClient.RenderJob(RenderJobId, WorkerRenderJobState.Queued));
    }

    private protected FakeDiskSpace Disk { get; } = new();

    private protected ApiFactory Factory { get; }

    private protected HttpClient Client { get; }

    private protected FakeWorkerClient Worker => Factory.Worker;

    private protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    private protected string AssetsDir => Path.Combine(Factory.DataDir, "assets");

    /// <summary>24-bit little-endian samples.</summary>
    public static byte[] Samples(int count, int value = 100_000)
    {
        var bytes = new byte[count * 3];
        for (var i = 0; i < count; i++)
        {
            bytes[i * 3] = (byte)value;
            bytes[(i * 3) + 1] = (byte)(value >> 8);
            bytes[(i * 3) + 2] = (byte)(value >> 16);
        }

        return bytes;
    }

    /// <summary>A canonical WAV of the data.</summary>
    public static byte[] Wav(byte[] data, int channels = 1, int bits = 24, int rate = 44_100)
    {
        var header = new byte[44];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)(36 + data.Length));
        "WAVEfmt "u8.CopyTo(header.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(22), (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24), (uint)rate);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(28), (uint)(rate * channels * bits / 8));
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(32), (ushort)(channels * bits / 8));
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(34), (ushort)bits);
        "data"u8.CopyTo(header.AsSpan(36));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(40), (uint)data.Length);
        return [.. header, .. data];
    }

    public static RecordingTakeUploadDto Metadata(long startSample = 44_100, long lengthSamples = 22_050) => new()
    {
        SessionId = Guid.Parse("5b0c3f2e-8a41-4d6b-9e07-1c2d3e4f5a6b"),
        Partial = false,
        StartSample = startSample,
        LengthSamples = lengthSamples,
        InputMode = InputChannelModeDto.Mono1,
        LatencySamples = 1102,
        LatencyMethod = LatencyMethodDto.Estimated,
        Part = RecordingPartDto.Bass,
        Speed = 1,
        TransposeSemitones = 0,
        Dropouts = 0,
        RecordedAt = ApiFactory.Start,
    };

    /// <summary>A multipart body; parts in the given order, metadata as raw JSON.</summary>
    public static MultipartFormDataContent Form(byte[]? wav, object? metadata, bool metadataFirst = false)
    {
        var form = new MultipartFormDataContent();
        var audio = wav is null ? null : new ByteArrayContent(wav);
        audio?.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        HttpContent? json = metadata switch
        {
            null => null,
            string text => new StringContent(text, System.Text.Encoding.UTF8, "application/json"),
            _ => JsonContent.Create(metadata, metadata.GetType(), options: ApiFactory.Json),
        };
        (HttpContent? Part, string Name)[] parts = metadataFirst ? [(json, "metadata"), (audio, "audio")] : [(audio, "audio"), (json, "metadata")];
        foreach (var (part, name) in parts)
        {
            if (part is not null)
            {
                form.Add(part, name, name == "audio" ? "take.wav" : "metadata.json");
            }
        }

        return form;
    }

    private protected async Task<Track> TrackAsync(string videoId = "dQw4w9WgXcQ") => await Factory.SeedAsync(AssetTestData.NamedTrack(videoId));

    private protected Task<HttpResponseMessage> UploadAsync(Guid trackId, HttpContent content) =>
        Client.PostAsync(new Uri(ApiRoutes.TrackTakesOf(trackId), UriKind.Relative), content, Ct);

    private protected async Task<RecordingTakeDto> CreateAsync(Guid trackId, RecordingTakeUploadDto? metadata = null, int value = 100_000)
    {
        var values = metadata ?? Metadata();
        using var response = await UploadAsync(trackId, Form(Wav(Samples((int)values.LengthSamples, value)), values));
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(Ct));
        return await response.BodyAsync<RecordingTakeDto>();
    }

    /// <summary>The worker completes every render with these bytes.</summary>
    private protected void CompleteRenders(byte[] flac) => AssetTestData.CompleteRenders(Worker, Convert.ToHexStringLower(SHA256.HashData(flac)), flac);

    private protected Task<T> InScopeAsync<T>(Func<StemStudioDbContext, Task<T>> action) =>
        Factory.WithScopeAsync(sp => action(sp.GetRequiredService<StemStudioDbContext>()));

    private protected Task<Asset> AssetAsync(Guid assetId) => InScopeAsync(context => context.Assets.AsNoTracking().SingleAsync(asset => asset.Id == assetId, Ct));

    private protected Task<List<RecordingTake>> TakesAsync() => InScopeAsync(context => context.RecordingTakes.AsNoTracking().ToListAsync(Ct));

    private protected string CapturePath(Guid assetId) => Path.Combine(AssetsDir, assetId.ToString("D"), "capture.wav");

    /// <summary>Folders below <c>assets/</c> (a refused upload leaves none).</summary>
    private protected string[] AssetFolders() => Directory.Exists(AssetsDir) ? Directory.GetDirectories(AssetsDir) : [];

    private protected static async Task<string> ProblemTypeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.TryGetProperty("type", out var type) ? type.GetString() ?? string.Empty : string.Empty;
    }

    private protected static string Problem(string code) => "https://stemstudio.local/problems/" + code;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Client.Dispose();
            Factory.Dispose();
        }
    }
}
