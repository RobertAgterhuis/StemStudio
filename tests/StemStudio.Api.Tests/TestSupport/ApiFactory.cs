using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StemStudio.Api.Generator;
using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>Hosts the API with a temp data directory (file SQLite), a fake worker and a fake clock.</summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset Start = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly bool _ownsDataDir;

    public ApiFactory(string? dataDir = null)
    {
        _ownsDataDir = dataDir is null;
        DataDir = dataDir ?? Directory.CreateTempSubdirectory("stemstudio-api-").FullName;
    }

    public string DataDir { get; }

    public FakeWorkerClient Worker { get; } = new();

    public FakeTimeProvider Clock { get; } = new(Start);

    /// <summary>The fake generator service behind the real generator client (feature 009).</summary>
    public FakeGenerator Generator { get; } = new();

    /// <summary>False: the generator client has no base URL (generation not installed).</summary>
    public bool GeneratorInstalled { get; init; } = true;

    public SequenceSeeds Seeds { get; } = new();

    public Action<IServiceCollection>? ConfigureServices { get; init; }

    public ListLoggerProvider Logs { get; } = new();

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);

    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public Task<Track?> FindTrackAsync(Guid id) =>
        WithScopeAsync(sp => sp.GetRequiredService<ITrackRepository>().FindAsync(id, CancellationToken.None));

    public Task<IReadOnlyList<Track>> AllTracksAsync() =>
        WithScopeAsync(sp => sp.GetRequiredService<ITrackRepository>().ListAsync(CancellationToken.None));

    public Task<Track> SeedAsync(Track track) => WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<ITrackRepository>().AddAsync(track, CancellationToken.None);
        return track;
    });

    public Task<Track> UpdateTrackAsync(Guid id, Action<Track> change) => WithScopeAsync(async sp =>
    {
        var tracks = sp.GetRequiredService<ITrackRepository>();
        var track = (await tracks.FindAsync(id, CancellationToken.None))!;
        change(track);
        await tracks.SaveAsync(CancellationToken.None);
        return track;
    });

    /// <summary>One pass of the standard job synchroniser in a fresh scope.</summary>
    public Task<bool> SyncTracksAsync() => WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<StemStudio.Api.Sync.ITrackSynchronizer>().SyncAllAsync(CancellationToken.None);
        return true;
    });

    /// <summary>One pass of the detail synchroniser in a fresh scope.</summary>
    public Task<bool> SyncDetailAsync() => WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<StemStudio.Api.Detail.DetailSynchronizer>().SyncAllAsync(CancellationToken.None);
        return true;
    });

    /// <summary>One pass of the metadata synchroniser in a fresh scope (feature 005).</summary>
    public Task<bool> SyncMetadataAsync() => WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<StemStudio.Api.Metadata.MetadataSynchronizer>().SyncAllAsync(CancellationToken.None);
        return true;
    });

    /// <summary>One pass of the lyrics synchroniser in a fresh scope (feature 006).</summary>
    public Task<bool> SyncLyricsAsync() => WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<StemStudio.Api.Lyrics.LyricsSynchronizer>().SyncAllAsync(CancellationToken.None);
        return true;
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.UseSetting("StemStudio:DataDir", DataDir);
        builder.UseSetting("StemStudio:WebOrigin", "http://web.test");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IWorkerClient>(Worker);
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<IGeneratorClient>(new GeneratorClient(GeneratorHttp(), NullLogger<GeneratorClient>.Instance, Clock));
            services.AddSingleton<StemStudio.Api.Generations.ISeedSource>(Seeds);
            ConfigureServices?.Invoke(services);
        });
    }

    /// <summary>One pass of the generation synchroniser in a fresh scope (feature 009).</summary>
    public Task<bool> SyncGenerationsAsync() => WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<StemStudio.Api.Generations.GenerationSynchronizer>().SyncAllAsync(CancellationToken.None);
        return true;
    });

    private HttpClient GeneratorHttp() =>
        new(Generator.Handler) { BaseAddress = GeneratorInstalled ? new Uri("http://generator:8010/") : null };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try
        {
            if (_ownsDataDir)
            {
                Directory.Delete(DataDir, true);
            }
        }
        catch (IOException)
        {
            // A file may still be locked briefly on Windows; the OS temp cleanup removes it later.
        }
    }
}

internal static class HttpResponseExtensions
{
    public static async Task<T> BodyAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json, TestContext.Current.CancellationToken))!;

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }

    public static async Task<string[]> FieldErrorsAsync(this HttpResponseMessage response, string field)
    {
        var json = await response.JsonAsync();
        return json.GetProperty("errors").GetProperty(field).EnumerateArray().Select(e => e.GetString()!).ToArray();
    }
}
