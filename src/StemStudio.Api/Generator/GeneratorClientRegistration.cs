using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace StemStudio.Api.Generator;

/// <summary>Registers the typed generator client with <c>Microsoft.Extensions.Http.Resilience</c>.</summary>
public static class GeneratorClientRegistration
{
    /// <summary>
    /// Adds <see cref="IGeneratorClient"/>: base address <c>StemStudio:GeneratorBaseUrl</c> (unset = not installed), GET
    /// retries only (a job or upload is never sent twice), a long attempt timeout for creating a job and a short one
    /// for everything else.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration holding the <c>StemStudio</c> section.</param>
    /// <returns>The HTTP client builder (tests replace the primary handler).</returns>
    public static IHttpClientBuilder AddGeneratorClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StemStudioApiOptions>().Bind(configuration.GetSection(StemStudioApiOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        var builder = services.AddHttpClient<IGeneratorClient, GeneratorClient>(ConfigureClient);
        builder.AddStandardResilienceHandler()
            .Configure((options, provider) => Resilience(options, provider.GetRequiredService<IOptions<StemStudioApiOptions>>().Value));
        return builder;
    }

    private static void ConfigureClient(IServiceProvider provider, HttpClient client)
    {
        var baseUrl = provider.GetRequiredService<IOptions<StemStudioApiOptions>>().Value.GeneratorBaseUrl;
        client.BaseAddress = baseUrl is null ? null : new Uri(baseUrl.AbsoluteUri.TrimEnd('/') + "/");
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    private static void Resilience(HttpStandardResilienceOptions options, StemStudioApiOptions api) =>
        ServiceResilience.Apply(options, api.GeneratorCreateTimeout, api.GeneratorPollTimeout);
}
