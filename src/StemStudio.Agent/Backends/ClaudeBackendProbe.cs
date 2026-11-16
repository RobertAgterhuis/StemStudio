using Anthropic.Exceptions;
using StemStudio.Agent.Model;
using StemStudio.Agent.Runtime;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Backends;

/// <summary>
/// Claude status without any request (key state from <see cref="Ports.IClaudeKeyProvider"/>, notice acknowledged, model), and the
/// explicit "Test connection": the free model lookup <c>GET /v1/models/{id}</c>, nothing generated (FR-019, FR-023, CK-FR-008).
/// </summary>
/// <param name="factory">Claude client factory.</param>
/// <param name="time">Clock (latency, 2 s budget).</param>
public sealed class ClaudeBackendProbe(ClaudeChatClientFactory factory, TimeProvider time)
{
    /// <summary>Budget of the explicit test.</summary>
    public static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(2);

    /// <summary>The Claude part of the status; sends nothing.</summary>
    /// <param name="settings">Agent settings.</param>
    /// <returns>The status (never the key).</returns>
    public ClaudeStatusDto Status(AgentSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var key = ClaudeKeyStatus.ToDto(factory.Keys.Resolve());
        return new ClaudeStatusDto
        {
            KeyConfigured = key.Configured,
            NoticeAcknowledged = settings.ClaudeNoticeAcknowledgedAt is not null,
            Model = settings.ClaudeModel,
            Key = key,
        };
    }

    /// <summary>Looks up the selected model on the user's explicit request.</summary>
    /// <param name="settings">Agent settings.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result.</returns>
    public async Task<BackendTestResultDto> TestAsync(AgentSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var started = time.GetTimestamp();
        var detail = await LookUpAsync(ClaudeChatClientFactory.ModelId(settings.ClaudeModel), cancellationToken);
        return new BackendTestResultDto
        {
            Backend = AgentBackendDto.Claude,
            Ok = detail.Ok,
            LatencyMs = (int)time.GetElapsedTime(started).TotalMilliseconds,
            Detail = detail.Text,
        };
    }

    private async Task<(bool Ok, string Text)> LookUpAsync(string model, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(Timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await factory.Client(factory.ActiveKey()).Models.Retrieve(model, cancellationToken: linked.Token);
            return (true, $"Claude answered; model {model} is available.");
        }
        catch (Exception exception) when (Failure(exception, cancellationToken) is { } text)
        {
            return (false, text);
        }
    }

    /// <summary>The readable text of a test failure; never the response body. Null rethrows (e.g. the caller cancelled).</summary>
    private static string? Failure(Exception exception, CancellationToken cancellationToken) => exception switch
    {
        BackendUnavailableException unavailable => unavailable.Message,
        AnthropicApiException api => ClaudeErrors.ForStatus(api.StatusCode),
        AnthropicIOException or HttpRequestException => ClaudeErrors.Unreachable,
        OperationCanceledException when !cancellationToken.IsCancellationRequested => ClaudeErrors.Unreachable,
        _ => null,
    };
}
