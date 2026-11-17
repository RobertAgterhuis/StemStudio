using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Recording;

/// <summary>Composition of feature 015 (play-along recording): options, repositories, services and the <c>recording</c> endpoints.</summary>
public static class RecordingRegistration
{
    /// <summary>Registers the 015 services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddRecording(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<RecordingOptions>(configuration.GetSection(RecordingOptions.SectionName));
        services.AddScoped<RecordingRepository>();
        services.AddScoped<IRecordingTakeRepository>(sp => sp.GetRequiredService<RecordingRepository>());
        services.AddScoped<IInputCalibrationRepository>(sp => sp.GetRequiredService<RecordingRepository>());
        services.AddScoped<TakeData>();
        services.AddSingleton<TakeLock>();
        services.AddSingleton<TakeFiles>();
        services.AddSingleton<TakeUploadReader>();
        services.AddSingleton<TakeDiskGuard>();
        services.AddSingleton<TakeRenderer>();
        services.AddSingleton<TakeCapture>();
        services.AddScoped<TakeUploadService>();
        services.AddScoped<TakeAudioReplacement>();
        services.AddScoped<TakeService>();
        services.AddScoped<TakeDeletion>();
        services.AddScoped<ITrackTakesCleanup>(sp => sp.GetRequiredService<TakeDeletion>());
        services.AddScoped<TakeAssetFinisher>();
        services.AddScoped<CalibrationService>();
        return services;
    }

    /// <summary>Maps the 015 endpoints (tag <c>recording</c>).</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapRecordingEndpoints(this IEndpointRouteBuilder app) => app.MapTakeEndpoints();
}
