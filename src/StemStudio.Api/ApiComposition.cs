using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StemStudio.Api.Agent;
using StemStudio.Api.Agent.Secrets;
using StemStudio.Api.Artwork;
using StemStudio.Api.Assets;
using StemStudio.Api.Detail;
using StemStudio.Api.Device;
using StemStudio.Api.Generations;
using StemStudio.Api.Generator;
using StemStudio.Api.Importing;
using StemStudio.Api.LibraryCare;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.Lyrics;
using StemStudio.Api.Metadata;
using StemStudio.Api.Mixes;
using StemStudio.Api.Scores;
using StemStudio.Api.Security;
using StemStudio.Api.Storage;
using StemStudio.Api.Styles;
using StemStudio.Api.Sync;
using StemStudio.Api.Tracks;
using StemStudio.Api.Voices;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Health;
using StemStudio.Domain.Styles;
using StemStudio.Persistence;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api;

/// <summary>Composition root of the API: services, middleware and endpoints.</summary>
public static class ApiComposition
{
    /// <summary>Name of the CORS policy for the web origin.</summary>
    public const string CorsPolicy = "web";

    /// <summary>Registers every API service.</summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder AddStemStudioApi(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = new StemStudioApiOptions();
        builder.Configuration.GetSection(StemStudioApiOptions.SectionName).Bind(options);
        var dataDirectory = DataDirectory.Resolve(options.DataDir, builder.Environment.ContentRootPath);
        var services = builder.Services;
        services.AddSingleton(dataDirectory);
        services.AddClaudeKeyProtection(builder.Configuration, builder.Environment.ContentRootPath, dataDirectory);
        var connectionString = LiveDatabaseFile.ForLive(ConnectionString(builder.Configuration, dataDirectory));
        services.AddStemStudioPersistence(connectionString);
        services.AddSingleton(new LiveDatabase(connectionString));
        AddServiceClients(services, builder.Configuration);
        AddApplicationServices(services);
        AddHttpServices(services, options);
        services.AddLibraryCare(options, builder.Environment.ContentRootPath);
        services.AddAgentHost(builder.Configuration);
        return builder;
    }

    private static void AddServiceClients(IServiceCollection services, IConfiguration configuration)
    {
        services.AddWorkerClient(configuration);
        services.AddGeneratorClient(configuration);
        services.AddSingerClient(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<LibraryGate>();
    }

    /// <summary>Creates the data directory, applies migrations and maps middleware and endpoints.</summary>
    /// <param name="app">The application.</param>
    /// <returns>A task.</returns>
    public static async Task UseStemStudioApiAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        Directory.CreateDirectory(app.Services.GetRequiredService<DataDirectory>().Root);
        await app.Services.RecoverLibraryCareAsync(CancellationToken.None);
        LiveDatabaseFile.EnsureCreated(app.Services.GetRequiredService<LiveDatabase>().ConnectionString);
        app.Services.GetRequiredService<KeyRingStatus>().LogStartupWarnings(app.Services.GetRequiredService<ILogger<KeyRingStatus>>());
        await using (var scope = app.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<StemStudioDbContext>().Database.MigrateAsync();
            await SeedPresetsAsync(scope.ServiceProvider);
            await scope.ServiceProvider.GetRequiredService<AudioHashIndex>().RefreshAsync(CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<AgentStartupRecovery>().RecoverAsync(CancellationToken.None);
        }

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseCors(CorsPolicy);
        app.UseCrossSiteRequestGuard();
        app.UseLibraryGate();
        app.MapOpenApi();
        MapEndpoints(app);
    }

    private static Task<int> SeedPresetsAsync(IServiceProvider services) =>
        services.GetRequiredService<IStyleRepository>().SeedPresetsAsync(
            PresetCatalog.Load().Presets, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime, CancellationToken.None);

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet(ApiRoutes.Health, () => TypedResults.Ok(new HealthResponse("ok"))).WithName("getHealth").WithTags("health");
        app.MapDeviceEndpoints();
        app.MapImportEndpoints();
        app.MapTrackEndpoints();
        app.MapStemEndpoints();
        app.MapDetailEndpoints();
        app.MapMixEndpoints();
        app.MapArtworkEndpoints();
        app.MapBandInfoEndpoints();
        app.MapAssetEndpoints();
        app.MapLyricsEndpoints();
        app.MapScoreEndpoints();
        app.MapGenerationEndpoints();
        app.MapStyleEndpoints();
        app.MapLyricsSheetEndpoints();
        app.MapVoiceEndpoints();
        app.MapAgentEndpoints();
        app.MapClaudeKeyEndpoints();
        app.MapLibraryCareEndpoints();
    }

    private static string ConnectionString(IConfiguration configuration, DataDirectory dataDirectory) =>
        configuration.GetConnectionString("StemStudio") ?? $"Data Source={Path.Combine(dataDirectory.Root, "stemstudio.db")}";

    private static void AddApplicationServices(IServiceCollection services)
    {
        services.AddSingleton<IStemFileStore, StemFileStore>();
        services.AddSingleton<IUploadStore, UploadStore>();
        services.AddScoped<WorkerCacheRehydrator>();
        services.AddScoped<ImportService>();
        services.AddScoped<TrackService>();
        services.AddScoped<TrackDeletionService>();
        services.AddScoped<StemService>();
        services.AddScoped<MixService>();
        AddSyncServices(services);
        AddFeatureServices(services);
        services.AddSingleton<ProcessingDeviceCache>();
        services.AddScoped<ProcessingDeviceService>();
    }

    private static void AddSyncServices(IServiceCollection services)
    {
        services.AddSingleton<TrackFiles>();
        services.AddSingleton<JobDeviceApplier>();
        services.AddScoped<JobReportApplier>();
        services.AddScoped<TrackJobApplier>();
        services.AddScoped<ITrackSynchronizer, TrackSynchronizer>();
        services.AddScoped<OrphanedImportSweeper>();
        services.AddHostedService<JobSyncService>();
    }

    private static void AddDetailServices(IServiceCollection services)
    {
        services.AddSingleton<IDetailStemFileStore, DetailStemFileStore>();
        services.AddScoped<DetailJobApplier>();
        services.AddScoped<DetailSynchronizer>();
        services.AddScoped<DetailService>();
    }

    private static void AddFeatureServices(IServiceCollection services)
    {
        AddDetailServices(services);
        AddArtworkServices(services);
        AddAssetServices(services);
        AddLyricsServices(services);
        AddScoreServices(services);
        AddGenerationServices(services);
    }

    private static void AddGenerationServices(IServiceCollection services)
    {
        services.AddSingleton<GenerationAvailability>();
        services.AddSingleton<SingerAvailability>();
        services.AddScoped<AudioHashIndex>();
        services.AddScoped<VoiceProfileService>();
        services.AddScoped<GeneratorRouter>();
        services.AddScoped<ConversionStemWaiter>();
        services.AddScoped<VoiceConversionService>();
        services.AddScoped<VoiceProfileDeletion>();
        services.TryAddSingleton<ISeedSource, RandomSeedSource>();
        services.AddScoped<GenerationSubmitter>();
        services.AddScoped<RestyleService>();
        services.AddScoped<SongService>();
        services.AddScoped<LyricsSheetService>();
        services.AddScoped<StyleService>();
        services.AddScoped<LibraryStyleGuard>();
        services.AddScoped<StyleEditingService>();
        services.AddScoped<GenerationStatusService>();
        services.AddScoped<RetryService>();
        services.AddScoped<GenerationService>();
        services.AddScoped<TakeImporter>();
        services.AddSingleton<TakeStorage>();
        services.AddScoped<IGeneratedAudioLibrary, GeneratedAudioLibraryAdapter>();
        services.AddScoped<TakeCollector>();
        services.AddScoped<GenerationSynchronizer>();
        services.AddHostedService<GenerationSyncService>();
    }

    private static void AddScoreServices(IServiceCollection services)
    {
        services.AddScoped<ScoreEstimator>();
        services.AddScoped<ScoreSourceResolver>();
        services.AddScoped<ScorePlanner>();
        services.AddScoped<ScoreSubmitter>();
        services.AddScoped<ScoreJobApplier>();
        services.AddScoped<ScoreService>();
        services.AddScoped<ScoreOptionsService>();
    }

    private static void AddLyricsServices(IServiceCollection services)
    {
        services.AddScoped<LyricsJobApplier>();
        services.AddScoped<LyricsAligner>();
        services.AddScoped<LyricsSynchronizer>();
        services.AddScoped<LyricsService>();
    }

    private static void AddArtworkServices(IServiceCollection services)
    {
        services.AddSingleton<IArtworkFileStore, ArtworkFileStore>();
        services.AddScoped<ArtworkDownloader>();
        services.AddScoped<ArtworkService>();
        services.AddScoped<UserArtworkService>();
        services.AddScoped<MetadataJobApplier>();
        services.AddScoped<MetadataSynchronizer>();
        services.AddScoped<MetadataService>();
    }

    private static void AddAssetServices(IServiceCollection services)
    {
        services.AddSingleton<IAssetFileStore, AssetFileStore>();
        services.TryAddSingleton<IDiskSpace, DriveDiskSpace>();
        services.AddSingleton<DiskSpaceGuard>();
        services.AddScoped<ExportPlanner>();
        services.AddScoped<AssetSubmitter>();
        services.AddScoped<ExportService>();
        services.AddScoped<AssetJobApplier>();
        services.AddScoped<AssetSynchronizer>();
        services.AddScoped<AssetService>();
        services.AddScoped<AssetLibraryService>();
        services.AddScoped<AssetRetryService>();
        services.AddScoped<StemPackService>();
    }

    private static void AddHttpServices(IServiceCollection services, StemStudioApiOptions options)
    {
        services.AddProblemDetails(problem => problem.CustomizeProblemDetails = ProblemDetailsCustomizer.Customize);
        services.AddExceptionHandler(handler => handler.StatusCodeSelector = ProblemDetailsCustomizer.StatusCodeFor);
        services.Configure<RouteHandlerOptions>(routes => routes.ThrowOnBadRequest = false);
        services.Configure<JsonOptions>(json => json.SerializerOptions.PropertyNameCaseInsensitive = true);
        services.AddOpenApi();
        services.AddCors(cors => cors.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins(options.WebOrigin)
            .WithMethods("GET")
            .WithHeaders("Range", "If-None-Match", "If-Modified-Since", "If-Range")
            .WithExposedHeaders("Content-Range", "Accept-Ranges", "ETag", "Last-Modified", "Content-Length")));
        services.Configure<KestrelServerOptions>(kestrel => kestrel.Limits.MaxRequestBodySize = UploadLimits.MaxRequestBodyBytes);
        services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = UploadLimits.MaxRequestBodyBytes);
    }
}
