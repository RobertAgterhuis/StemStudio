using Microsoft.AspNetCore.HostFiltering;
using StemStudio.Api.Agent.Secrets;

namespace StemStudio.Api.Security;

/// <summary>
/// Host allow-list of the API (refactor 003 step 2, ADR-027 D6 DNS rebinding): ASP.NET Core host filtering answers 400 to a
/// request whose <c>Host</c> is not in <c>StemStudio:AllowedHosts</c> (semicolon-separated, ports ignored, any case; blank =
/// the local names of <see cref="KeyEntryPolicy.LocalHosts"/>). An empty <c>Host</c> is refused too. The standard top-level
/// <c>AllowedHosts</c> key is not used: the list is set explicitly, so it is never the framework's <c>*</c>.
/// </summary>
public static class ApiExposure
{
    /// <summary>Configuration key of the host allow-list (environment <c>StemStudio__AllowedHosts</c>).</summary>
    public const string AllowedHostsKey = "StemStudio:AllowedHosts";

    /// <summary>Configures host filtering from <see cref="AllowedHostsKey"/>.</summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApiExposure(this IServiceCollection services)
    {
        services.AddOptions<HostFilteringOptions>().Configure<IConfiguration>(Configure);
        return services;
    }

    /// <summary>The configured host names, or the local names when the value is blank or holds no name.</summary>
    /// <param name="configuration">Configuration.</param>
    /// <returns>The allowed host names.</returns>
    public static IReadOnlyList<string> AllowedHosts(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var hosts = (configuration[AllowedHostsKey] ?? string.Empty).Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return hosts.Length == 0 ? KeyEntryPolicy.LocalHosts : hosts;
    }

    private static void Configure(HostFilteringOptions options, IConfiguration configuration)
    {
        options.AllowedHosts = [.. AllowedHosts(configuration)];
        options.AllowEmptyHosts = false;
    }
}
