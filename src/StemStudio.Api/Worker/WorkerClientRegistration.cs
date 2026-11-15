namespace StemStudio.Api.Worker;

/// <summary>Registers the typed worker client with <c>Microsoft.Extensions.Http.Resilience</c>.</summary>
public static class WorkerClientRegistration
{
    /// <summary>
    /// Adds <see cref="IWorkerClient"/> with the standard resilience pipeline: retries (3, exponential from 200 ms) for
    /// GET only (never POST, so a job or upload is never sent twice), per-request attempt timeouts
    /// (<see cref="WorkerTimeouts"/>: long for creating a job, short for polls), a total timeout and a circuit breaker.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration holding the <c>StemStudio</c> section.</param>
    /// <returns>The HTTP client builder (tests replace the primary handler).</returns>
    public static IHttpClientBuilder AddWorkerClient(this IServiceCollection services, IConfiguration configuration) =>
        services.AddServiceClient<IWorkerClient, WorkerClient>(
            configuration, api => new ServiceClientSettings(api.WorkerBaseUrl, api.WorkerCreateJobTimeout, api.WorkerPollTimeout));
}
