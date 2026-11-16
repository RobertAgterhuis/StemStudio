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
    public static IHttpClientBuilder AddGeneratorClient(this IServiceCollection services, IConfiguration configuration) =>
        services.AddServiceClient<IGeneratorClient, GeneratorClient>(
            configuration, api => new ServiceClientSettings(api.GeneratorBaseUrl, api.GeneratorCreateTimeout, api.GeneratorPollTimeout));

    /// <summary>Adds <see cref="ISingerClient"/>: base address <c>StemStudio:SingerBaseUrl</c> (unset = voice features not installed).</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration holding the <c>StemStudio</c> section.</param>
    /// <returns>The HTTP client builder.</returns>
    public static IHttpClientBuilder AddSingerClient(this IServiceCollection services, IConfiguration configuration) =>
        services.AddServiceClient<ISingerClient, SingerClient>(
            configuration, api => new ServiceClientSettings(api.SingerBaseUrl, api.GeneratorCreateTimeout, api.GeneratorPollTimeout));
}
