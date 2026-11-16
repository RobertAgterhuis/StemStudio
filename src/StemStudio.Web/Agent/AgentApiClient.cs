using System.Globalization;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Api;

namespace StemStudio.Web.Agent;

/// <summary>
/// <see cref="HttpClient"/>-based <see cref="IAgentApiClient"/>, registered as a typed client on the server-side API
/// base URL. The event stream is read with <see cref="SseReader"/> and resumed after a dropped connection with
/// <c>Last-Event-ID</c>; after <see cref="MaxReconnects"/> failures in a row it throws <see cref="ApiError.Unreachable"/>.
/// </summary>
/// <param name="http">The configured HTTP client.</param>
public sealed class AgentApiClient(HttpClient http) : IAgentApiClient
{
    /// <summary>Consecutive stream failures tolerated before giving up.</summary>
    public const int MaxReconnects = 3;

    /// <summary>JSON options of the agent contract (camelCase, nulls omitted; enums carry their own converters).</summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly ApiTransport _transport = new(http, Json);

    /// <summary>Wait before a reconnect (tests use zero).</summary>
    internal TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    public Task<AgentStatusDto> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Get<AgentStatusDto>(ApiRoutes.AgentStatus, cancellationToken);

    /// <inheritdoc />
    public Task<AgentSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Get<AgentSettingsDto>(ApiRoutes.AgentSettings, cancellationToken);

    /// <inheritdoc />
    public Task<AgentSettingsDto> UpdateSettingsAsync(UpdateAgentSettingsRequest request, CancellationToken cancellationToken = default) =>
        _transport.SendAsync<AgentSettingsDto>(HttpMethod.Put, ApiRoutes.AgentSettings, Body(request), cancellationToken);

    /// <inheritdoc />
    public Task<BackendTestResultDto> TestBackendAsync(AgentBackendDto backend, CancellationToken cancellationToken = default) =>
        Post<BackendTestResultDto>(ApiRoutes.AgentSettingsTest, new TestAgentBackendRequest { Backend = backend }, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentToolInfoDto>> ListToolsAsync(string? page = null, Guid? trackId = null, CancellationToken cancellationToken = default) =>
        Get<IReadOnlyList<AgentToolInfoDto>>(ApiRoutes.AgentTools + Query(("page", page), ("trackId", trackId?.ToString())), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ConversationSummaryDto>> ListConversationsAsync(
        int limit = AgentContractLimits.ConversationListDefault, DateTimeOffset? before = null, CancellationToken cancellationToken = default) =>
        Get<IReadOnlyList<ConversationSummaryDto>>(
            ApiRoutes.AgentConversations + Query(("limit", limit.ToString(CultureInfo.InvariantCulture)), ("before", before?.ToString("O", CultureInfo.InvariantCulture))),
            cancellationToken);

    /// <inheritdoc />
    public Task<ConversationDto> CreateConversationAsync(string? title = null, CancellationToken cancellationToken = default) =>
        Post<ConversationDto>(ApiRoutes.AgentConversations, new CreateConversationRequest { Title = title }, cancellationToken);

    /// <inheritdoc />
    public Task<ConversationDto> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        Get<ConversationDto>(ApiRoutes.AgentConversation(conversationId), cancellationToken);

    /// <inheritdoc />
    public Task<ConversationSummaryDto> RenameConversationAsync(Guid conversationId, string title, CancellationToken cancellationToken = default) =>
        _transport.SendAsync<ConversationSummaryDto>(
            HttpMethod.Patch, ApiRoutes.AgentConversation(conversationId), Body(new RenameConversationRequest { Title = title }), cancellationToken);

    /// <inheritdoc />
    public Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        _transport.SendAsync(HttpMethod.Delete, ApiRoutes.AgentConversation(conversationId), null, cancellationToken);

    /// <inheritdoc />
    public Task<AgentMessageAcceptedDto> SendMessageAsync(Guid conversationId, SendAgentMessageRequest request, CancellationToken cancellationToken = default) =>
        Post<AgentMessageAcceptedDto>(ApiRoutes.AgentMessages(conversationId), request, cancellationToken);

    /// <inheritdoc />
    public Task<AgentRunAcceptedDto> UndoAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        _transport.SendAsync<AgentRunAcceptedDto>(HttpMethod.Post, ApiRoutes.AgentUndo(conversationId), null, cancellationToken);

    /// <inheritdoc />
    public Task<AgentRunDto> GetRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
        Get<AgentRunDto>(ApiRoutes.AgentRun(runId), cancellationToken);

    /// <inheritdoc />
    public Task<ApprovalStateDto> DecideApprovalAsync(Guid runId, Guid stepId, DecideApprovalRequest request, CancellationToken cancellationToken = default) =>
        Post<ApprovalStateDto>(ApiRoutes.AgentApproval(runId, stepId), request, cancellationToken);

    /// <inheritdoc />
    public Task SubmitUiResultAsync(Guid runId, Guid stepId, SubmitUiResultRequest request, CancellationToken cancellationToken = default) =>
        _transport.SendAsync(HttpMethod.Post, ApiRoutes.AgentUiResult(runId, stepId), Body(request), cancellationToken);

    /// <inheritdoc />
    public Task<AgentRunDto> CancelRunAsync(Guid runId, bool cancelJobs, CancellationToken cancellationToken = default) =>
        Post<AgentRunDto>(ApiRoutes.AgentCancel(runId), new CancelAgentRunRequest { CancelJobs = cancelJobs }, cancellationToken);

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentEvent> StreamRunEventsAsync(Guid runId, int? after = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var cursor = new StreamCursor(after);
        while (!cursor.Ended)
        {
            await cursor.WaitBeforeReconnectAsync(ReconnectDelay, cancellationToken);
            await foreach (var e in ConnectAsync(runId, cursor, cancellationToken))
            {
                yield return e;
            }
        }
    }

    private static JsonContent Body<T>(T value) => JsonContent.Create(value, options: Json);

    private static string Query(params (string Name, string? Value)[] parameters)
    {
        var present = parameters.Where(p => p.Value is not null).Select(p => $"{p.Name}={Uri.EscapeDataString(p.Value!)}").ToList();
        return present.Count == 0 ? string.Empty : "?" + string.Join('&', present);
    }

    private static AgentEvent ToEvent(SseMessage message) => new(
        int.Parse(message.Id ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture),
        message.Event,
        JsonSerializer.Deserialize<JsonElement>(message.Data, Json));

    private Task<T> Get<T>(string uri, CancellationToken ct) => _transport.SendAsync<T>(HttpMethod.Get, uri, null, ct);

    private Task<T> Post<T>(string uri, object body, CancellationToken ct) => _transport.SendAsync<T>(HttpMethod.Post, uri, Body(body), ct);

    private async IAsyncEnumerable<AgentEvent> ConnectAsync(Guid runId, StreamCursor cursor, [EnumeratorCancellation] CancellationToken ct)
    {
        using var response = await OpenAsync(cursor.CreateRequest(runId), cursor, ct);
        if (response is null)
        {
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var messages = SseReader.ReadAsync(stream, ct).GetAsyncEnumerator(ct);
        while (await cursor.MoveNextAsync(messages))
        {
            var e = ToEvent(messages.Current);
            cursor.Seen(e);
            yield return e;
        }
    }

    private async Task<HttpResponseMessage?> OpenAsync(HttpRequestMessage request, StreamCursor cursor, CancellationToken ct)
    {
        try
        {
            return await _transport.SendRawAsync(request, ct, HttpCompletionOption.ResponseHeadersRead);
        }
        catch (ApiException ex) when (ReferenceEquals(ex.Error, ApiError.Unreachable))
        {
            cursor.Failed();
            return null;
        }
    }

    /// <summary>Resume state of one event stream: the last event id, consecutive failures and whether it ended.</summary>
    private sealed class StreamCursor(int? after)
    {
        private int _connections;
        private int _failures;

        public int? LastId { get; private set; } = after;

        public bool Ended { get; private set; }

        public HttpRequestMessage CreateRequest(Guid runId)
        {
            var resume = _connections++ > 0;
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(ApiRoutes.AgentRunEvents(runId, resume ? null : LastId), UriKind.Relative));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            if (resume && LastId is { } last)
            {
                request.Headers.Add("Last-Event-ID", last.ToString(CultureInfo.InvariantCulture));
            }

            return request;
        }

        public async Task WaitBeforeReconnectAsync(TimeSpan delay, CancellationToken ct)
        {
            if (_failures > MaxReconnects)
            {
                throw new ApiException(ApiError.Unreachable);
            }

            if (_connections > 0)
            {
                await Task.Delay(delay, ct);
            }
        }

        public async Task<bool> MoveNextAsync(IAsyncEnumerator<SseMessage> messages)
        {
            if (!Ended && await TryMoveAsync(messages))
            {
                return true;
            }

            if (!Ended)
            {
                Failed();
            }

            return false;
        }

        public void Seen(AgentEvent e)
        {
            LastId = e.Id;
            Ended = e.IsTerminal;
            _failures = 0;
        }

        public void Failed() => _failures++;

        private static async Task<bool> TryMoveAsync(IAsyncEnumerator<SseMessage> messages)
        {
            try
            {
                return await messages.MoveNextAsync();
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
