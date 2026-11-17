namespace StemStudio.Api.Recording;

/// <summary>
/// Composition hook of feature 015 (play-along recording). Stub added by the 014 backend lane so the
/// composition builds; the 015 backend lane owns this file and fills it.
/// </summary>
public static class RecordingRegistration
{
    /// <summary>Registers the 015 services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddRecording(this IServiceCollection services, IConfiguration configuration) => services;

    /// <summary>Maps the 015 endpoints (tag <c>recording</c>).</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapRecordingEndpoints(this IEndpointRouteBuilder app) => app;
}
