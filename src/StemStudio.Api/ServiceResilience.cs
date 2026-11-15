using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace StemStudio.Api;

/// <summary>Base URL and attempt timeouts of a typed service client.</summary>
/// <param name="BaseUrl">The service URL; null = not installed (no base address).</param>
/// <param name="CreateTimeout">Attempt timeout of POST requests.</param>
/// <param name="PollTimeout">Attempt timeout of every other request.</param>
public sealed record ServiceClientSettings(Uri? BaseUrl, TimeSpan CreateTimeout, TimeSpan PollTimeout);

/// <summary>The registration and resilience settings shared by the worker and generator clients.</summary>
public static class ServiceResilience
{
    /// <summary>Registers a typed client whose base address and timeouts come from <see cref="StemStudioApiOptions"/>.</summary>
    /// <typeparam name="TClient">The client interface.</typeparam>
    /// <typeparam name="TImplementation">The client.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration holding the <c>StemStudio</c> section.</param>
    /// <param name="settings">Selects the client's settings from the options.</param>
    /// <returns>The HTTP client builder (tests replace the primary handler).</returns>
    public static IHttpClientBuilder AddServiceClient<TClient, TImplementation>(
        this IServiceCollection services, IConfiguration configuration, Func<StemStudioApiOptions, ServiceClientSettings> settings)
        where TClient : class
        where TImplementation : class, TClient
    {
        services.AddOptions<StemStudioApiOptions>().Bind(configuration.GetSection(StemStudioApiOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        var builder = services.AddHttpClient<TClient, TImplementation>((provider, client) => Configure(client, settings(Options(provider))));
        builder.AddStandardResilienceHandler().Configure((options, provider) =>
        {
            var selected = settings(Options(provider));
            Apply(options, selected.CreateTimeout, selected.PollTimeout);
        });
        return builder;
    }

    /// <summary>
    /// Retries (3, exponential from 200 ms) for GET only (never POST, so a job or upload is never sent twice), a long
    /// attempt timeout for POST and a short one for everything else, a total timeout and a circuit breaker that cover
    /// the longest attempt.
    /// </summary>
    /// <param name="options">The standard resilience options.</param>
    /// <param name="createTimeout">Attempt timeout of POST requests.</param>
    /// <param name="pollTimeout">Attempt timeout of every other request.</param>
    public static void Apply(HttpStandardResilienceOptions options, TimeSpan createTimeout, TimeSpan pollTimeout)
    {
        ArgumentNullException.ThrowIfNull(options);
        var longest = createTimeout + pollTimeout;
        options.Retry.DisableForUnsafeHttpMethods();
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        options.AttemptTimeout.Timeout = longest;
        options.AttemptTimeout.TimeoutGenerator = args =>
            ValueTask.FromResult(args.Context.GetRequestMessage()?.Method == HttpMethod.Post ? createTimeout : pollTimeout);
        options.TotalRequestTimeout.Timeout = longest * 2;
        options.CircuitBreaker.SamplingDuration = longest * 3;
    }

    private static StemStudioApiOptions Options(IServiceProvider provider) => provider.GetRequiredService<IOptions<StemStudioApiOptions>>().Value;

    private static void Configure(HttpClient client, ServiceClientSettings settings)
    {
        client.BaseAddress = settings.BaseUrl is { } url ? new Uri(url.AbsoluteUri.TrimEnd('/') + "/") : null;
        client.Timeout = Timeout.InfiniteTimeSpan;
    }
}
