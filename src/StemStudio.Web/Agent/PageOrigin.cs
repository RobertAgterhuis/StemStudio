using Microsoft.AspNetCore.Components;

namespace StemStudio.Web.Agent;

/// <summary>
/// The host name the page of this circuit was opened under (011 follow-up claude-key, CK-FR-012). The Claude key controls
/// are enabled only under a loopback host name (<c>localhost</c>, <c>127.0.0.1</c>, <c>[::1]</c>); any IP address, the
/// compose service name <c>web</c> or another name disables them. This is a UI guard: the API's endpoint filter
/// (<c>403 local-only</c>) is the enforcing check. The host of the first read is kept for the circuit.
/// </summary>
/// <param name="navigation">The circuit's navigation manager; its base URI carries the host the browser used.</param>
public sealed class PageOrigin(NavigationManager navigation)
{
    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "[::1]"];

    private string? _host;

    /// <summary>The host name of the page (as the browser sent it, without the port).</summary>
    public string Host => _host ??= new Uri(navigation.BaseUri).Host;

    /// <summary>True when the page was opened under a loopback host name.</summary>
    public bool IsLocal => IsLoopbackHost(Host);

    /// <summary>True for exactly the loopback host names <c>localhost</c>, <c>127.0.0.1</c> and <c>[::1]</c> (case-insensitive).</summary>
    /// <param name="host">A host name without port, or null.</param>
    /// <returns>Whether it is a loopback host name.</returns>
    public static bool IsLoopbackHost(string? host) => host is not null && LoopbackHosts.Contains(host, StringComparer.OrdinalIgnoreCase);
}
