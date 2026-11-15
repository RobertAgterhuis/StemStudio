using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

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
    public static IHttpClientBuilder AddWorkerClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StemStudioApiOptions>().Bind(configuration.GetSection(StemStudioApiOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        var builder = services.AddHttpClient<IWorkerClient, WorkerClient>(ConfigureClient);
        builder.AddStandardResilienceHandler()
            .Configure((options, provider) => ConfigureResilience(options, provider.GetRequiredService<IOptions<StemStudioApiOptions>>().Value));
        return builder;
    }

    private static void ConfigureClient(IServiceProvider provider, HttpClient client)
    {
        var baseUrl = provider.GetRequiredService<IOptions<StemStudioApiOptions>>().Value.WorkerBaseUrl;
        client.BaseAddress = new Uri(baseUrl.AbsoluteUri.TrimEnd('/') + "/");
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    private static void ConfigureResilience(HttpStandardResilienceOptions options, StemStudioApiOptions api) =>
        ServiceResilience.Apply(options, api.WorkerCreateJobTimeout, api.WorkerPollTimeout);
}
