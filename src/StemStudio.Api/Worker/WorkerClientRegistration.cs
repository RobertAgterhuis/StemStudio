using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace StemStudio.Api.Worker;

/// <summary>Registers the typed worker client with <c>Microsoft.Extensions.Http.Resilience</c>.</summary>
public static class WorkerClientRegistration
{
    /// <summary>
    /// Adds <see cref="IWorkerClient"/> with the standard resilience pipeline: retries (3, exponential from 200 ms) for
    /// GET only (never POST, so a job or upload is never sent twice), 2-minute attempt and 5-minute total timeouts and a
    /// circuit breaker.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration holding the <c>StemStudio</c> section.</param>
    /// <returns>The HTTP client builder (tests replace the primary handler).</returns>
    public static IHttpClientBuilder AddWorkerClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StemStudioApiOptions>().Bind(configuration.GetSection(StemStudioApiOptions.SectionName));
        var builder = services.AddHttpClient<IWorkerClient, WorkerClient>(ConfigureClient);
        builder.AddStandardResilienceHandler(ConfigureResilience);
        return builder;
    }

    private static void ConfigureClient(IServiceProvider provider, HttpClient client)
    {
        var baseUrl = provider.GetRequiredService<IOptions<StemStudioApiOptions>>().Value.WorkerBaseUrl;
        client.BaseAddress = new Uri(baseUrl.AbsoluteUri.TrimEnd('/') + "/");
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    private static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.Retry.DisableForUnsafeHttpMethods();
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
    }
}
