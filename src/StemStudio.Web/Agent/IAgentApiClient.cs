using StemStudio.Contracts.Agent;
using StemStudio.Web.Api;

namespace StemStudio.Web.Agent;

/// <summary>
/// Typed client for the composer agent endpoints <c>/api/agent/*</c> (backend API 1.10.0, contracts/DELTA.md section 1;
/// the Claude key calls of 1.21.0, 011 follow-up claude-key).
/// Every method throws <see cref="ApiException"/> with the mapped problem details on failure; the API key is never
/// part of any request or response.
/// </summary>
public interface IAgentApiClient
{
    /// <summary><c>GET /api/agent/status</c>: backend readiness, reason and fix.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The status.</returns>
    Task<AgentStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/agent/settings</c>.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The settings.</returns>
    Task<AgentSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/agent/settings</c>; null fields stay unchanged.</summary>
    /// <param name="request">The changes.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The stored settings.</returns>
    Task<AgentSettingsDto> UpdateSettingsAsync(UpdateAgentSettingsRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/agent/settings/test</c>: connection test without generating text.</summary>
    /// <param name="backend">Ollama or Claude.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The test result.</returns>
    Task<BackendTestResultDto> TestBackendAsync(AgentBackendDto backend, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/agent/settings/claude-key</c> (1.21.0): the key status; never the key.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The key status.</returns>
    Task<ClaudeKeyStatusDto> GetClaudeKeyStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /api/agent/settings/claude-key</c> (1.21.0, local-only, write-only): stores or replaces the key, encrypted.
    /// Never contacts Anthropic and does not change the selected backend.
    /// </summary>
    /// <param name="apiKey">The key as typed; it is sent once and never kept or logged by the client.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The resulting key status.</returns>
    Task<ClaudeKeyStatusDto> StoreClaudeKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/agent/settings/claude-key</c> (1.21.0, local-only, idempotent): removes the stored key.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The resulting key status.</returns>
    Task<ClaudeKeyStatusDto> DeleteClaudeKeyAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/agent/tools</c>: the registry view for a page context.</summary>
    /// <param name="page">The page name, or null.</param>
    /// <param name="trackId">The open track, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tools.</returns>
    Task<IReadOnlyList<AgentToolInfoDto>> ListToolsAsync(string? page = null, Guid? trackId = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/agent/conversations</c>, newest first.</summary>
    /// <param name="limit">1-100.</param>
    /// <param name="before">Only conversations updated before this time, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The summaries.</returns>
    Task<IReadOnlyList<ConversationSummaryDto>> ListConversationsAsync(
        int limit = AgentContractLimits.ConversationListDefault, DateTimeOffset? before = null, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/agent/conversations</c>.</summary>
    /// <param name="title">The title, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new conversation.</returns>
    Task<ConversationDto> CreateConversationAsync(string? title = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/agent/conversations/{id}</c>: messages and run summaries.</summary>
    /// <param name="conversationId">The conversation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The conversation.</returns>
    Task<ConversationDto> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary><c>PATCH /api/agent/conversations/{id}</c>.</summary>
    /// <param name="conversationId">The conversation.</param>
    /// <param name="title">The new title.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The renamed summary.</returns>
    Task<ConversationSummaryDto> RenameConversationAsync(Guid conversationId, string title, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/agent/conversations/{id}</c> (<c>409 run-active</c> while its run is active).</summary>
    /// <param name="conversationId">The conversation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when deleted.</returns>
    Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/agent/conversations/{id}/messages</c>: starts a run.</summary>
    /// <param name="conversationId">The conversation.</param>
    /// <param name="request">The text and page context.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The run and message ids.</returns>
    Task<AgentMessageAcceptedDto> SendMessageAsync(Guid conversationId, SendAgentMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/agent/conversations/{id}/undo</c>: undoes the last agent action group.</summary>
    /// <param name="conversationId">The conversation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The undo run.</returns>
    Task<AgentRunAcceptedDto> UndoAsync(Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/agent/runs/{id}</c>: the run snapshot.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The run.</returns>
    Task<AgentRunDto> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /api/agent/runs/{id}/events</c>: the run's events until the terminal event; a dropped connection is
    /// resumed with <c>Last-Event-ID</c>.
    /// </summary>
    /// <param name="runId">The run.</param>
    /// <param name="after">Only events with a larger sequence number, or null for all.</param>
    /// <param name="cancellationToken">Stops streaming.</param>
    /// <returns>The events in order.</returns>
    IAsyncEnumerable<AgentEvent> StreamRunEventsAsync(Guid runId, int? after = null, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/agent/runs/{id}/approvals/{stepId}</c>.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="stepId">The step awaiting approval.</param>
    /// <param name="request">The decision bound to the arguments hash.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The stored approval state.</returns>
    Task<ApprovalStateDto> DecideApprovalAsync(Guid runId, Guid stepId, DecideApprovalRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/agent/runs/{id}/ui-results/{stepId}</c>: the result of a UI tool.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="stepId">The UI step.</param>
    /// <param name="request">The result.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when accepted.</returns>
    Task SubmitUiResultAsync(Guid runId, Guid stepId, SubmitUiResultRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/agent/runs/{id}/cancel</c>.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancelJobs">Also cancel the jobs the run started.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The run snapshot.</returns>
    Task<AgentRunDto> CancelRunAsync(Guid runId, bool cancelJobs, CancellationToken cancellationToken = default);
}
