using Microsoft.AspNetCore.HttpLogging;
using StemStudio.Contracts;

namespace StemStudio.Api.Agent.Secrets;

/// <summary>
/// Turns HTTP logging off for the Claude key route before anything is logged (011 follow-up claude-key, research R-06). The
/// route's <c>WithHttpLogging(None)</c> metadata is only visible after routing; this interceptor also covers a host that
/// puts <c>UseHttpLogging</c> in front of routing, so neither the request body nor its headers are ever logged.
/// </summary>
public sealed class KeyEndpointLoggingInterceptor : IHttpLoggingInterceptor
{
    private static readonly PathString KeyRoute = new(ApiRoutes.AgentClaudeKey);

    /// <summary>True for the key route (any case, with or without a trailing slash).</summary>
    /// <param name="path">Request path.</param>
    /// <returns>True when nothing of the request may be logged.</returns>
    public static bool IsKeyRoute(PathString path) =>
        path.StartsWithSegments(KeyRoute, StringComparison.OrdinalIgnoreCase, out var remaining) && (!remaining.HasValue || remaining == "/");

    /// <inheritdoc />
    public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext)
    {
        ArgumentNullException.ThrowIfNull(logContext);
        Silence(logContext);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnResponseAsync(HttpLoggingInterceptorContext logContext)
    {
        ArgumentNullException.ThrowIfNull(logContext);
        Silence(logContext);
        return ValueTask.CompletedTask;
    }

    private static void Silence(HttpLoggingInterceptorContext logContext)
    {
        if (IsKeyRoute(logContext.HttpContext.Request.Path))
        {
            logContext.LoggingFields = HttpLoggingFields.None;
        }
    }
}
