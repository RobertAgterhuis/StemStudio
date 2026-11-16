using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>
/// <c>GET /api/agent/status</c> (FR-023, SC-005): the agent library's probe, answered within 3 s. A probe that hangs, fails or is
/// not registered yields a "not ready" status instead of an error, so the rest of the app is never affected; a not-ready
/// status turns <c>POST messages</c> into 503 <c>backend-unavailable</c>.
/// </summary>
/// <param name="services">Resolves the optional <see cref="IAgentStatusService"/>.</param>
/// <param name="settings">Agent settings (backend of the fallback status).</param>
/// <param name="clock">Clock of the 3 s limit.</param>
public sealed class AgentStatusProbe(IServiceProvider services, IAgentSettingsStore settings, TimeProvider clock)
{
    /// <summary>Reason when the probe did not answer in time.</summary>
    public const string TimeoutReason = "The agent backend did not answer within 3 seconds.";

    /// <summary>Reason when the probe failed or the agent library is missing.</summary>
    public const string FailureReason = "The agent backend could not be checked.";

    /// <summary>Longest wait for the probe.</summary>
    public static readonly TimeSpan Limit = TimeSpan.FromSeconds(3);

    /// <summary>The current status.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The status (never the key).</returns>
    public async Task<AgentStatusDto> GetAsync(CancellationToken cancellationToken)
    {
        if (services.GetService<IAgentStatusService>() is not { } status)
        {
            return await FallbackAsync(FailureReason, cancellationToken);
        }

        try
        {
            return await status.GetStatusAsync(cancellationToken).WaitAsync(Limit, clock, cancellationToken);
        }
        catch (TimeoutException)
        {
            return await FallbackAsync(TimeoutReason, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return await FallbackAsync(FailureReason, cancellationToken);
        }
    }

    /// <summary>
    /// Tests one backend on the user's explicit request (<c>POST /api/agent/settings/test</c>). Not capped at 3 s (a model may
    /// need to load), but a failing or missing agent library yields <c>ok: false</c> with a fixed detail, never an exception text.
    /// </summary>
    /// <param name="backend">ollama or claude.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The test result.</returns>
    public async Task<BackendTestResultDto> TestAsync(AgentBackendDto backend, CancellationToken cancellationToken)
    {
        try
        {
            if (services.GetService<IAgentStatusService>() is { } status)
            {
                return await status.TestAsync(backend, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Falls through to the fixed failure result.
        }

        return new BackendTestResultDto { Backend = backend, Ok = false, LatencyMs = 0, Detail = FailureReason };
    }

    /// <summary>503 <c>backend-unavailable</c> with the status, or null when the agent is ready.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The problem, or null.</returns>
    public async Task<IResult?> UnavailableAsync(CancellationToken cancellationToken)
    {
        var status = await GetAsync(cancellationToken);
        return status.Ready ? null : TypedResults.Problem(
            status.Reason ?? FailureReason,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "The agent is not available",
            type: AgentProblemTypes.Uri(AgentProblemTypes.BackendUnavailable),
            extensions: new Dictionary<string, object?> { ["agentStatus"] = status });
    }

    private async Task<AgentStatusDto> FallbackAsync(string reason, CancellationToken cancellationToken) =>
        Fallback(await settings.GetAsync(cancellationToken), reason);

    private static AgentStatusDto Fallback(AgentSettings current, string reason) => new()
    {
        Backend = current.Backend,
        Ready = false,
        UsingFallback = false,
        Reason = reason,
        Ollama = new OllamaStatusDto { Reachable = false, DefaultPresent = false, FallbackPresent = false },
        Claude = new ClaudeStatusDto { KeyConfigured = false, NoticeAcknowledged = current.ClaudeNoticeAcknowledgedAt is not null, Model = current.ClaudeModel },
    };
}
