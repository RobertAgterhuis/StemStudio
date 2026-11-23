using System.Net;
using StemStudio.Api.Security;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent.Secrets;

/// <summary>
/// Local-only rule of the Claude key writes (011 follow-up claude-key, CK-FR-012, DELTA.md section 4): the peer is loopback or an
/// address of the internal caller, the <c>Host</c> is a loopback name or <c>api</c>, and an <c>Origin</c>, when sent, is the
/// Web app's. Runs before the handler reads the body; a refusal answers 403 <c>local-only</c> and is logged at most once a
/// minute with the reason and the peer address, never the body. Before feature 018 the host check is a soft layer (plan T7).
/// </summary>
/// <param name="policy">Web origin.</param>
/// <param name="peers">Loopback or the internal caller (shared with the app-wide guard, refactor 003).</param>
/// <param name="clock">Clock of the log rate limit.</param>
/// <param name="logger">Logger.</param>
public sealed partial class LocalOnlyKeyEntryFilter(KeyEntryPolicy policy, TrustedPeerPolicy peers, TimeProvider clock, ILogger<LocalOnlyKeyEntryFilter> logger)
    : IEndpointFilter
{
    /// <summary>Detail of 403 <c>local-only</c>.</summary>
    public const string Detail = "The Claude key can only be entered on the computer StemStudio runs on.";

    private static readonly TimeSpan LogInterval = TimeSpan.FromMinutes(1);
    private readonly Lock _gate = new();
    private DateTimeOffset? _lastLog;

    /// <summary>403 <c>local-only</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult Refusal() => AgentProblems.Coded(StatusCodes.Status403Forbidden, AgentProblemTypes.LocalOnly, "Only on this computer", Detail);

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var http = context.HttpContext;
        if (await RefusalReasonAsync(http) is not { } reason)
        {
            return await next(context);
        }

        LogRateLimited(reason, http.Connection.RemoteIpAddress);
        return Refusal();
    }

    /// <summary>Why a request is not local (<c>peer</c>, <c>host</c>, <c>origin</c>), or null when it is.</summary>
    /// <param name="http">The request context.</param>
    /// <returns>The reason or null.</returns>
    public async Task<string?> RefusalReasonAsync(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (!await peers.IsTrustedAsync(http.Connection.RemoteIpAddress, http.RequestAborted))
        {
            return "peer";
        }

        if (!KeyEntryPolicy.IsAllowedHost(http.Request.Host.Host))
        {
            return "host";
        }

        return http.Request.Headers.Origin is { Count: > 0 } origin && !policy.IsAllowedOrigin(origin.ToString()) ? "origin" : null;
    }

    private void LogRateLimited(string reason, IPAddress? peer)
    {
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            if (_lastLog is { } last && now - last < LogInterval)
            {
                return;
            }

            _lastLog = now;
        }

        LogRefused(logger, reason, peer);
    }

    [LoggerMessage(EventId = 4105, EventName = "KeyEntryRefused", Level = LogLevel.Information,
        Message = "Claude key entry refused (reason: {Reason}, peer {Peer})")]
    private static partial void LogRefused(ILogger logger, string reason, IPAddress? peer);
}
