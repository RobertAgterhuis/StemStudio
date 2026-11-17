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
/// App-wide cross-site request guard (security review 013 item 12): a state-changing request (not GET, HEAD or OPTIONS)
/// under <c>/api</c> is refused with 403 when its <c>Origin</c> header is present and is not the Web app's origin
/// (<c>StemStudio:WebOrigin</c>), or when the browser marks it <c>Sec-Fetch-Site: cross-site</c>. Requests without an
/// <c>Origin</c> (server-side Blazor calls, Playwright API calls, curl) pass, so body-less POSTs such as
/// <c>revertRestore</c> and <c>verifyBackup</c> cannot be fired from another site as simple requests.
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
        if (IsRefused(context, context.RequestServices.GetRequiredService<KeyEntryPolicy>()))
        {
            await TypedResults.Problem(Detail, statusCode: StatusCodes.Status403Forbidden, title: "Cross-site request refused").ExecuteAsync(context);
            return;
        }

        await next(context);
    });

    /// <summary>True when the request is a cross-site state change.</summary>
    /// <param name="context">The request.</param>
    /// <param name="policy">Holds the Web app's origin.</param>
    /// <returns>True when refused.</returns>
    public static bool IsRefused(HttpContext context, KeyEntryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);
        var request = context.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method)
            || !request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || context.GetEndpoint()?.Metadata.GetMetadata<OriginCheckedByEndpoint>() is not null)
        {
            return false;
        }

        var origin = request.Headers.Origin.ToString();
        return (origin.Length > 0 && !policy.IsAllowedOrigin(origin))
            || string.Equals(request.Headers["Sec-Fetch-Site"].ToString(), "cross-site", StringComparison.OrdinalIgnoreCase);
    }
}
