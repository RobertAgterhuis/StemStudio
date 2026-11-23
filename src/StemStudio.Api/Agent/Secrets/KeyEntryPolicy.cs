using System.Net;
using System.Net.Sockets;

namespace StemStudio.Api.Agent.Secrets;

/// <summary>
/// Configuration of the local-only rule (011 follow-up claude-key, DELTA.md section 4): the trusted internal caller (compose:
/// the <c>web</c> service) and the browser origin of the Web app.
/// </summary>
public sealed class KeyEntryPolicy
{
    /// <summary>Configuration key of the internal caller (environment <c>StemStudio__Agent__KeyEntry__InternalCaller</c>).</summary>
    public const string InternalCallerKey = "StemStudio:Agent:KeyEntry:InternalCaller";

    private readonly string _webOrigin;

    /// <summary>Creates the policy.</summary>
    /// <param name="internalCaller">Host name whose addresses are accepted besides loopback; blank = none.</param>
    /// <param name="webOrigin">The only accepted <c>Origin</c> header value (<c>StemStudio:WebOrigin</c>).</param>
    public KeyEntryPolicy(string? internalCaller, string webOrigin)
    {
        InternalCaller = string.IsNullOrWhiteSpace(internalCaller) ? null : internalCaller.Trim();
        _webOrigin = (webOrigin ?? string.Empty).TrimEnd('/');
    }

    /// <summary>
    /// The local host names: <c>localhost</c>, <c>127.0.0.1</c>, <c>[::1]</c> and <c>api</c> (the compose service name). The only
    /// names key entry accepts, and the default of the API's host allow-list (refactor 003, <c>ApiExposure</c>).
    /// </summary>
    public static IReadOnlyList<string> LocalHosts { get; } = ["localhost", "127.0.0.1", "[::1]", "api"];

    /// <summary>Host name of the trusted internal caller, or null.</summary>
    public string? InternalCaller { get; }

    /// <summary>True for <c>localhost</c>, <c>127.0.0.1</c>, <c>[::1]</c> and <c>api</c> (port ignored, any case).</summary>
    /// <param name="host">The host part of the <c>Host</c> header.</param>
    /// <returns>True when allowed.</returns>
    public static bool IsAllowedHost(string host) => LocalHosts.Contains(host, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the origin equals the Web app's origin (trailing slash and case ignored).</summary>
    /// <param name="origin">The <c>Origin</c> header value.</param>
    /// <returns>True when allowed.</returns>
    public bool IsAllowedOrigin(string origin) =>
        string.Equals((origin ?? string.Empty).TrimEnd('/'), _webOrigin, StringComparison.OrdinalIgnoreCase);

    /// <summary>The address in its IPv4 form when it is an IPv4-mapped IPv6 address.</summary>
    /// <param name="address">An address.</param>
    /// <returns>The normalised address.</returns>
    public static IPAddress Normalise(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }
}

/// <summary>Resolves a host name to its addresses (seam for the internal caller of the local-only rule).</summary>
public interface IHostAddressResolver
{
    /// <summary>The addresses of a host; empty when it cannot be resolved.</summary>
    /// <param name="host">Host name.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The addresses.</returns>
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}

/// <summary>DNS implementation of <see cref="IHostAddressResolver"/> (the compose network's resolver in Docker).</summary>
public sealed class DnsHostAddressResolver : IHostAddressResolver
{
    /// <inheritdoc />
    public async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            return await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException)
        {
            return [];
        }
    }
}
