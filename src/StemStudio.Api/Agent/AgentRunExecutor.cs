using System.Threading.Channels;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Stores;
using StemStudio.Contracts.Agent;
using StemStudio.Api.LibraryCare.Gate;

namespace StemStudio.Api.Agent;

/// <summary>Texts of the run executor.</summary>
public static class AgentRunExecutorText
{
    /// <summary>Readable failure of a run whose agent threw (never the exception text: no paths, URLs or stack traces).</summary>
    public const string InternalFailure = "The agent stopped because of an internal error. Try the command again.";
}

/// <summary>
/// Hosted executor of queued runs (feature 011, plan.md "Key flows" 1): runs are executed one at a time through the agent
/// library's <see cref="IAgentRunService"/>; an unexpected exception fails the run with <c>internal</c>; when the run has ended
/// the conversation no longer points at it.
/// </summary>
/// <param name="scopes">Scope per run.</param>
/// <param name="lifecycle">Run end bookkeeping.</param>
/// <param name="logger">Logger.</param>
public sealed partial class AgentRunExecutor(IServiceScopeFactory scopes, AgentRunLifecycle lifecycle, ILogger<AgentRunExecutor> logger) : BackgroundService
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>Queues a run for execution.</summary>
    /// <param name="runId">Run id.</param>
    public void Enqueue(Guid runId) => _queue.Writer.TryWrite(runId);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await RunAsync(runId, stoppingToken);
        }
    }

    private async Task RunAsync(Guid runId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await (scope.ServiceProvider.GetService<LibraryGate>()?.WaitWhileMaintenanceAsync(stoppingToken) ?? Task.CompletedTask);
            await scope.ServiceProvider.GetRequiredService<IAgentRunService>().ExecuteAsync(runId, stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            LogRunFailed(logger, runId, exception);
            await lifecycle.FailAsync(runId, AgentFailureCodes.Internal, AgentRunExecutorText.InternalFailure, stoppingToken);
        }

        await lifecycle.ReleaseIfEndedAsync(runId, stoppingToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Agent run {RunId} failed")]
    private static partial void LogRunFailed(ILogger logger, Guid runId, Exception exception);
}

/// <summary>End-of-run bookkeeping shared by the executor and the approval and cancel endpoints.</summary>
/// <param name="runs">Run store.</param>
/// <param name="conversations">Conversation store.</param>
/// <param name="events">Event sink.</param>
/// <param name="clock">Clock.</param>
public sealed class AgentRunLifecycle(IRunStore runs, IConversationStore conversations, IRunEventSink events, TimeProvider clock)
{
    /// <summary>Fails a run that is not terminal yet and publishes <c>run.failed</c>.</summary>
    /// <param name="runId">Run id.</param>
    /// <param name="code">Failure code.</param>
    /// <param name="message">Readable message.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task FailAsync(Guid runId, string code, string message, CancellationToken cancellationToken)
    {
        var run = await runs.GetRunAsync(runId, cancellationToken);
        if (run is null || run.IsTerminal)
        {
            return;
        }

        // The terminal event is stored before the run turns terminal, so a stream that sees the ended run also replays it.
        await events.PublishAsync(runId, AgentEventTypes.RunFailed, AgentStoreValues.ToJson(new RunFailureDto { Code = code, Message = message }), cancellationToken);
        await runs.UpdateRunAsync(run with { Status = AgentRunStatusDto.Failed, FailureCode = code, FailureMessage = message, EndedAt = clock.GetUtcNow() }, cancellationToken);
    }

    /// <summary>Clears the conversation's active run once the run is terminal.</summary>
    /// <param name="runId">Run id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task ReleaseIfEndedAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await runs.GetRunAsync(runId, cancellationToken);
        if (run is not { IsTerminal: true } || await conversations.GetAsync(run.ConversationId, cancellationToken) is not { } conversation)
        {
            return;
        }

        if (conversation.ActiveRunId == runId)
        {
            await conversations.UpdateAsync(conversation with { ActiveRunId = null, UpdatedAt = clock.GetUtcNow() }, cancellationToken);
        }
    }
}
