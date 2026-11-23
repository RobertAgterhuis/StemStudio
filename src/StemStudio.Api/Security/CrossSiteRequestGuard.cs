using StemStudio.Api.Agent.Secrets;

namespace StemStudio.Api.Security;

/// <summary>Endpoint metadata: the endpoint checks the <c>Origin</c> itself with a stricter rule and its own problem (local-only key entry).</summary>
public sealed class OriginCheckedByEndpoint
{
    /// <summary>The shared instance.</summary>
    public static readonly OriginCheckedByEndpoint Instance = new();

    private OriginCheckedByEndpoint()
    {
    }
}

/// <summary>
/// App-wide cross-site request guard (security review 013 item 12, refactor 003 step 3): a state-changing request (not GET,
/// HEAD or OPTIONS) under <c>/api</c> is refused with 403 when the browser marks it <c>Sec-Fetch-Site: cross-site</c>, when
/// its <c>Origin</c> header is present and is not the Web app's origin (<c>StemStudio:WebOrigin</c>), or when it has no
/// (or an empty) <c>Origin</c> and the peer is not trusted (<see cref="TrustedPeerPolicy"/>: loopback or the internal caller).
/// So server-side Blazor calls, the agent's loopback client and a script on this computer still work, a LAN device with
/// curl does not, and body-less POSTs such as <c>revertRestore</c> cannot be fired from another site as simple requests.
/// </summary>
public static class CrossSiteRequestGuard
{
    /// <summary>Detail of the 403 problem.</summary>
    public const string Detail = "Requests that change data are accepted from the StemStudio web app only.";

    /// <summary>Adds the guard (after routing, so endpoint metadata is known).</summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static IApplicationBuilder UseCrossSiteRequestGuard(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var services = context.RequestServices;
        if (await IsRefusedAsync(context, services.GetRequiredService<KeyEntryPolicy>(), services.GetRequiredService<TrustedPeerPolicy>()))
        {
            await TypedResults.Problem(Detail, statusCode: StatusCodes.Status403Forbidden, title: "Cross-site request refused").ExecuteAsync(context);
            return;
        }

        await next(context);
    });

    /// <summary>True when the request is a state change from another site or, without an <c>Origin</c>, from an untrusted peer.</summary>
    /// <param name="context">The request.</param>
    /// <param name="policy">Holds the Web app's origin.</param>
    /// <param name="peers">Decides whether an Origin-less caller is local.</param>
    /// <returns>True when refused.</returns>
    public static async Task<bool> IsRefusedAsync(HttpContext context, KeyEntryPolicy policy, TrustedPeerPolicy peers)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(peers);
        if (!IsGuarded(context))
        {
            return false;
        }

        var origin = context.Request.Headers.Origin.ToString();
        return IsCrossSiteFetch(context.Request)
            || (origin.Length > 0 ? !policy.IsAllowedOrigin(origin) : !await peers.IsTrustedAsync(context.Connection.RemoteIpAddress, context.RequestAborted));
    }

    private static bool IsGuarded(HttpContext context) =>
        IsStateChanging(context.Request.Method)
        && context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && context.GetEndpoint()?.Metadata.GetMetadata<OriginCheckedByEndpoint>() is null;

    private static bool IsStateChanging(string method) =>
        !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method);

    private static bool IsCrossSiteFetch(HttpRequest request) =>
        string.Equals(request.Headers["Sec-Fetch-Site"].ToString(), "cross-site", StringComparison.OrdinalIgnoreCase);
}
