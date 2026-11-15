using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StemStudio.Api.Importing;
using StemStudio.Api.Mixes;
using StemStudio.Api.Storage;
using StemStudio.Api.Sync;
using StemStudio.Api.Tracks;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Health;
using StemStudio.Persistence;

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
        services.AddStemStudioPersistence(ConnectionString(builder.Configuration, dataDirectory));
        services.AddWorkerClient(builder.Configuration);
        services.TryAddSingleton(TimeProvider.System);
        AddApplicationServices(services);
        AddHttpServices(services, options);
        return builder;
    }

    /// <summary>Creates the data directory, applies migrations and maps middleware and endpoints.</summary>
    /// <param name="app">The application.</param>
    /// <returns>A task.</returns>
    public static async Task UseStemStudioApiAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        Directory.CreateDirectory(app.Services.GetRequiredService<DataDirectory>().Root);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<StemStudioDbContext>().Database.MigrateAsync();
        }

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseCors(CorsPolicy);
        app.MapOpenApi();
        MapEndpoints(app);
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet(ApiRoutes.Health, () => TypedResults.Ok(new HealthResponse("ok"))).WithName("getHealth").WithTags("health");
        app.MapImportEndpoints();
        app.MapTrackEndpoints();
        app.MapStemEndpoints();
        app.MapMixEndpoints();
    }

    private static string ConnectionString(IConfiguration configuration, DataDirectory dataDirectory) =>
        configuration.GetConnectionString("StemStudio") ?? $"Data Source={Path.Combine(dataDirectory.Root, "stemstudio.db")}";

    private static void AddApplicationServices(IServiceCollection services)
    {
        services.AddSingleton<IStemFileStore, StemFileStore>();
        services.AddSingleton<IDetailStemFileStore, DetailStemFileStore>();
        services.AddSingleton<IUploadStore, UploadStore>();
        services.AddScoped<ImportService>();
        services.AddScoped<TrackService>();
        services.AddScoped<TrackDeletionService>();
        services.AddScoped<StemService>();
        services.AddScoped<MixService>();
        services.AddScoped<TrackJobApplier>();
        services.AddScoped<ITrackSynchronizer, TrackSynchronizer>();
        services.AddScoped<OrphanedImportSweeper>();
        services.AddHostedService<JobSyncService>();
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
