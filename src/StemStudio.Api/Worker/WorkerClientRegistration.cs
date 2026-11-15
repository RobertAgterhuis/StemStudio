using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

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

    private static void ConfigureResilience(HttpStandardResilienceOptions options, StemStudioApiOptions api)
    {
        // The static attempt timeout bounds every generated one; the total and sampling windows must cover it.
        var longest = api.WorkerCreateJobTimeout + api.WorkerPollTimeout;
        options.Retry.DisableForUnsafeHttpMethods();
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        options.AttemptTimeout.Timeout = longest;
        options.AttemptTimeout.TimeoutGenerator = args => ValueTask.FromResult(WorkerTimeouts.For(args.Context.GetRequestMessage(), api));
        options.TotalRequestTimeout.Timeout = longest * 2;
        options.CircuitBreaker.SamplingDuration = longest * 3;
    }
}
