using StemStudio.Api.Sync;
using StemStudio.Api.Worker;

namespace StemStudio.Api.InstrumentViews;

/// <summary>
/// Composition hook of feature 016 (instrument views): the worker analysis client, the analysis service, job applier and
/// auto rules. The only place 016 registers or maps anything.
/// </summary>
public static class InstrumentViewsRegistration
{
    /// <summary>Registers the 016 services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddInstrumentViews(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<InstrumentViewsOptions>().Bind(configuration.GetSection(InstrumentViewsOptions.SectionName));
        services.AddServiceClient<IWorkerAnalysisClient, WorkerClient>(
            configuration, api => new ServiceClientSettings(api.WorkerBaseUrl, api.WorkerCreateJobTimeout, api.WorkerPollTimeout));
        services.AddSingleton<AnalysisGate>();
        services.AddSingleton<StemFingerprinter>();
        services.AddScoped<AnalysisEstimator>();
        services.AddScoped<AnalysisRunFactory>();
        services.AddScoped<TrackAnalysisService>();
        services.AddScoped<ChordService>();
        services.AddScoped<TrackViewsProjection>();
        services.AddScoped<Scores.ScoreChordSource>();
        services.AddScoped<AnalysisJobApplier>();
        services.AddScoped<AnalysisAutoStarter>();
        services.AddScoped<ITrackCompletionHook>(provider => provider.GetRequiredService<AnalysisAutoStarter>());
        services.AddScoped<IDetailCompletionHook>(provider => provider.GetRequiredService<AnalysisAutoStarter>());
        return services;
    }

    /// <summary>Maps the 016 endpoints (tag <c>instrument-views</c>).</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapInstrumentViewEndpoints(this IEndpointRouteBuilder app) => app.MapChordEndpoints().MapDrumHitEndpoints().MapCatalogueEndpoint();
}
