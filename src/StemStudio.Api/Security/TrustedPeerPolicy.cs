using System.Net;
using StemStudio.Api.Agent.Secrets;

namespace StemStudio.Api.Security;

/// <summary>
/// Who counts as "this computer or the Web app" at the TCP level (refactor 003 step 3): a loopback peer, or an address of the
/// configured internal caller (<see cref="KeyEntryPolicy.InternalCaller"/>, compose: the <c>web</c> service, resolved on every
/// call so a recreated container is recognised). Only the socket's remote address counts: forwarded headers are never read.
/// Shared by <see cref="CrossSiteRequestGuard"/> (Origin-less writes) and <see cref="LocalOnlyKeyEntryFilter"/> (key entry).
/// </summary>
/// <param name="policy">Holds the internal caller.</param>
/// <param name="resolver">Resolves the internal caller.</param>
public sealed class TrustedPeerPolicy(KeyEntryPolicy policy, IHostAddressResolver resolver)
{
    /// <summary>True when the peer is loopback (IPv4-mapped included) or an address of the internal caller.</summary>
    /// <param name="peer">The connection's remote address; null (no TCP peer) is never trusted.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when trusted.</returns>
    public async Task<bool> IsTrustedAsync(IPAddress? peer, CancellationToken cancellationToken)
    {
        if (peer is null)
        {
            return false;
        }

        var address = KeyEntryPolicy.Normalise(peer);
        if (IPAddress.IsLoopback(address) || policy.InternalCaller is not { } caller)
        {
            return IPAddress.IsLoopback(address);
        }

        var known = await resolver.ResolveAsync(caller, cancellationToken);
        return known.Select(KeyEntryPolicy.Normalise).Contains(address);
    }
}
