using StemStudio.Api.ScoreFollow;
using StemStudio.Api.SongSections;
using StemStudio.Api.Sync;
using StemStudio.Api.Worker;

namespace StemStudio.Api.Practice;

/// <summary>Options of feature 014 (section <c>StemStudio:Practice</c>, data-model 1).</summary>
public sealed class PracticeFlowOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "StemStudio:Practice";

    /// <summary>Start section detection when a separation completes (default true).</summary>
    public bool SectionDetectOnComplete { get; set; } = true;

    /// <summary>How long the setlist PDF waits for the worker, 5..120 s (default 30).</summary>
    public int SetlistPdfTimeoutSeconds { get; set; } = 30;

    /// <summary>The PDF timeout clamped to 5..120 s.</summary>
    public TimeSpan SetlistPdfTimeout => TimeSpan.FromSeconds(Math.Clamp(SetlistPdfTimeoutSeconds, 5, 120));
}

/// <summary>
/// Composition hook of feature 014 (practice flow): the worker practice client, sections, score views, setlists and the
/// practice log. The only place 014 registers or maps anything.
/// </summary>
public static class PracticeFlowRegistration
{
    /// <summary>Registers the 014 services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddPracticeFlow(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PracticeFlowOptions>().Bind(configuration.GetSection(PracticeFlowOptions.SectionName));
        services.AddServiceClient<IWorkerPracticeClient, WorkerClient>(
            configuration, api => new ServiceClientSettings(api.WorkerBaseUrl, api.WorkerCreateJobTimeout, api.WorkerPollTimeout));
        services.AddSingleton<SectionDetectionGate>();
        services.AddScoped<SectionService>();
        services.AddScoped<SectionJobApplier>();
        services.AddScoped<ITrackCompletionHook, SectionDetectTrigger>();
        services.AddSingleton<ScoreViewGate>();
        services.AddSingleton<ScoreViewFiles>();
        services.AddScoped<ScoreViewService>();
        services.AddScoped<ScoreViewJobApplier>();
        services.AddScoped<IAssetReadyHook, ScoreViewReadyHook>();
        return services;
    }

    /// <summary>Maps the 014 endpoints (tags <c>sections</c>, <c>score-follow</c>, <c>setlists</c>, <c>practice</c>).</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapPracticeFlowEndpoints(this IEndpointRouteBuilder app) => app.MapSectionEndpoints().MapScoreFollowEndpoints();
}
