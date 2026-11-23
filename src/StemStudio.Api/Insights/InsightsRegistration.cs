using StemStudio.Api.Handouts.Sources;
using StemStudio.Api.Sync;
using StemStudio.Api.Worker;

namespace StemStudio.Api.Insights;

/// <summary>
/// Composition hook of feature 020 (insights and handouts): the worker insights client, the quality report, handouts, practice
/// notes and the 014/016 source adapters. The only place 020 registers or maps anything.
/// </summary>
public static class InsightsRegistration
{
    /// <summary>Registers the 020 services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddInsights(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<InsightsOptions>().Bind(configuration.GetSection(InsightsOptions.SectionName));
        services.AddServiceClient<IWorkerInsightsClient, WorkerClient>(
            configuration, api => new ServiceClientSettings(api.WorkerBaseUrl, api.WorkerCreateJobTimeout, api.WorkerPollTimeout));
        return services.AddQuality().AddHandoutSources();
    }

    /// <summary>Maps the 020 endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapInsightsEndpoints(this IEndpointRouteBuilder app) => app.MapQualityEndpoints();

    private static IServiceCollection AddQuality(this IServiceCollection services)
    {
        services.AddSingleton<QualityGate>();
        services.AddSingleton<AnalyzerVersionCache>();
        services.AddScoped<QualityInputs>();
        services.AddScoped<QualityTips>();
        services.AddScoped<QualityView>();
        services.AddScoped<QualityService>();
        services.AddScoped<QualityJobApplier>();
        services.AddScoped<QualityTrigger>();
        services.AddScoped<ITrackCompletionHook>(provider => provider.GetRequiredService<QualityTrigger>());
        return services.AddScoped<IDetailCompletionHook>(provider => provider.GetRequiredService<QualityTrigger>());
    }

    private static IServiceCollection AddHandoutSources(this IServiceCollection services)
    {
        services.AddScoped<ISectionSource, SectionSourceAdapter>();
        services.AddScoped<ISetlistSource, SetlistSourceAdapter>();
        services.AddScoped<IPracticeLogSource, PracticeLogSourceAdapter>();
        services.AddScoped<IChordSource, ChordSourceAdapter>();
        return services.AddScoped<INotationCatalog, NotationCatalogAdapter>();
    }
}
