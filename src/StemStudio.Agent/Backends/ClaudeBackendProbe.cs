using Anthropic.Exceptions;
using StemStudio.Agent.Model;
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

    /// <summary>Detail when the stored key is requested but none is readable.</summary>
    public const string NoStoredKey = "There is no readable stored Claude key to test: save the key in Settings first.";

    /// <summary>Detail when the environment key is requested but none is usable.</summary>
    public const string NoEnvironmentKey = "ANTHROPIC_API_KEY is not set to a usable Claude key, so there is nothing to test.";

    /// <summary>Looks up the selected model with the active key on the user's explicit request.</summary>
    /// <param name="settings">Agent settings.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result.</returns>
    public Task<BackendTestResultDto> TestAsync(AgentSettings settings, CancellationToken cancellationToken) =>
        TestAsync(settings, ClaudeKeyTestSourceDto.Active, cancellationToken);

    /// <summary>
    /// Looks up the selected model with the key of the requested source on the user's explicit request (CK-FR-007): exactly
    /// one request, or none when that source has no usable key. <c>keySource</c> names the key that was tested.
    /// </summary>
    /// <param name="settings">Agent settings.</param>
    /// <param name="source">Which key to test; <see cref="ClaudeKeyTestSourceDto.Active"/> follows the precedence.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result (never the key).</returns>
    public async Task<BackendTestResultDto> TestAsync(AgentSettings settings, ClaudeKeyTestSourceDto source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var started = time.GetTimestamp();
        var selected = factory.Keys.KeyFor(source);
        var (ok, text) = selected is null
            ? (false, Missing(source))
            : await LookUpAsync(selected.Key, ClaudeChatClientFactory.ModelId(settings.ClaudeModel), cancellationToken);
        var latency = (int)time.GetElapsedTime(started).TotalMilliseconds;
        return new BackendTestResultDto { Backend = AgentBackendDto.Claude, Ok = ok, LatencyMs = latency, Detail = text, KeySource = selected?.Source };
    }

    private string Missing(ClaudeKeyTestSourceDto source)
    {
        var resolution = factory.Keys.Resolve();
        return source switch
        {
            ClaudeKeyTestSourceDto.Stored => NoStoredKey,
            ClaudeKeyTestSourceDto.Environment => NoEnvironmentKey,
            _ => ClaudeErrors.KeyUnusable(resolution.Reason, resolution.Fix),
        };
    }

    private async Task<(bool Ok, string Text)> LookUpAsync(ClaudeApiKey key, string model, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(Timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await factory.Client(key).Models.Retrieve(model, cancellationToken: linked.Token);
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
        AnthropicApiException api => ClaudeErrors.ForStatus(api.StatusCode),
        AnthropicIOException or HttpRequestException => ClaudeErrors.Unreachable,
        OperationCanceledException when !cancellationToken.IsCancellationRequested => ClaudeErrors.Unreachable,
        _ => null,
    };
}
