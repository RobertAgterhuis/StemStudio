using Microsoft.Extensions.AI;
using StemStudio.Agent.Model;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>
/// The agent's own tool loop (ADR-020, research R-02): one model turn at a time, each call through the policy gate and the
/// step executor, bounded by the run limits (FR-011). Every state change is persisted and published as a DELTA.md event.
/// </summary>
public sealed class AgentRunner
{
    private readonly AgentServices _services;
    private readonly RunEvents _events;
    private readonly ModelTurnStreamer _streamer;
    private readonly StepExecutor _steps;
    private readonly ServiceStatusReader _status;

    /// <summary>Creates the runner.</summary>
    /// <param name="services">Ports and services.</param>
    public AgentRunner(AgentServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _events = new RunEvents(services.Events);
        _streamer = new ModelTurnStreamer(_events, services.Time);
        _steps = new StepExecutor(services, _events);
        _status = new ServiceStatusReader(services.Actions.Generations, services.Time);
    }

    /// <summary>Executes a queued run until it completes or fails; terminal and unknown runs are left alone.</summary>
    /// <param name="runId">Run identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task ExecuteAsync(Guid runId, CancellationToken cancellationToken)
    {
        var state = await RunLoader.LoadAsync(_services, runId, cancellationToken);
        if (state is null)
        {
            return;
        }

        var outcome = RunOutcome.Internal;
        try
        {
            await SaveAsync(state, state.Run with { Status = AgentRunStatusDto.Running }, cancellationToken);
            outcome = await LoopAsync(state, cancellationToken);
        }
        catch (RunStopException stop)
        {
            LogCause(state, stop.InnerException);
            outcome = RunOutcome.Fail(stop.Code, stop.Message);
        }
        catch (OperationCanceledException)
        {
            outcome = RunOutcome.Cancelled;
        }
        catch (Exception exception)
        {
            LogCause(state, exception);
            outcome = RunOutcome.Internal;
        }
        finally
        {
            await EndAsync(state, outcome);
        }
    }

    private async Task<RunOutcome> LoopAsync(RunState state, CancellationToken cancellationToken)
    {
        while (true)
        {
            var calls = await TurnAsync(state, cancellationToken);
            if (calls.Count == 0)
            {
                var message = await RunLoader.AppendAnswerAsync(_services, state, cancellationToken);
                return RunOutcome.Completed(message.Id);
            }

            await ExecuteCallsAsync(state, calls, cancellationToken);
        }
    }

    private void LogCause(RunState state, Exception? exception)
    {
        if (exception is not null)
        {
            RunLog.RunFailed(_services.Logger, state.RunId, exception);
        }
    }

    private async Task<IReadOnlyList<FunctionCallContent>> TurnAsync(RunState state, CancellationToken cancellationToken)
    {
        if (state.Run.ModelTurns >= state.Limits.MaxModelTurns)
        {
            throw new RunStopException(AgentFailureCodes.LimitReached, $"The run reached the limit of {state.Limits.MaxModelTurns} model turns.");
        }

        var turn = await _services.Chat.NextTurnAsync(new ChatTurnContext(state.Run, state.Command.Context), cancellationToken);
        using var limit = new TurnLimit(turn.TimeoutSeconds ?? state.Limits.TurnTimeoutSeconds, _services.Time, cancellationToken);
        await AnnounceModelAsync(state, turn, cancellationToken);
        var tools = await OfferedToolsAsync(state, cancellationToken);
        var request = TurnBuilder.Build(Input(state, tools), turn.Options);
        var response = await _streamer.StreamAsync(state.RunId, turn, request, limit);
        await RecordTurnAsync(state, response, cancellationToken);
        return [.. response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>()];
    }

    private async Task<IReadOnlyList<AgentToolDescriptor>> OfferedToolsAsync(RunState state, CancellationToken cancellationToken) =>
        _services.Registry.For(await _status.ContextAsync(state.Context, cancellationToken));

    private static TurnInput Input(RunState state, IReadOnlyList<AgentToolDescriptor> tools) => new()
    {
        History = state.History,
        Command = state.Command.Text,
        Context = state.Context,
        Transcript = state.Transcript,
        Tools = tools,
    };

    private async Task AnnounceModelAsync(RunState state, ChatTurn turn, CancellationToken cancellationToken)
    {
        if (state.Model is null)
        {
            await SaveAsync(state, state.Run with { Model = turn.Model }, cancellationToken);
            var started = new RunStartedEventDto { RunId = state.RunId, Backend = state.Run.Backend, Model = turn.Model, UsingFallback = turn.UsingFallback };
            await _events.PublishAsync(state.RunId, AgentEventTypes.RunStarted, started, cancellationToken);
        }
        else if (state.Model != turn.Model)
        {
            var changed = new ModelChangedEventDto { Model = turn.Model, UsingFallback = turn.UsingFallback, Reason = turn.ChangeReason ?? "The model changed." };
            await _events.PublishAsync(state.RunId, AgentEventTypes.ModelChanged, changed, cancellationToken);
        }

        state.Model = turn.Model;
    }

    private async Task RecordTurnAsync(RunState state, ChatResponse response, CancellationToken cancellationToken)
    {
        state.Transcript.AddRange(response.Messages);
        state.Answer.Append(response.Text);
        var usage = response.Usage is { } details ? new TokenUsage(details.InputTokenCount ?? 0, details.OutputTokenCount ?? 0, details.CachedInputTokenCount ?? 0) : null;
        await SaveAsync(state, state.Run with { ModelTurns = state.Run.ModelTurns + 1, Usage = usage is null ? state.Run.Usage : state.Run.Usage.Add(usage) }, cancellationToken);
        if (usage is not null)
        {
            var total = state.Run.Usage;
            var dto = new TokenUsageDto { InputTokens = total.InputTokens, OutputTokens = total.OutputTokens, CachedInputTokens = total.CachedInputTokens };
            await _events.PublishAsync(state.RunId, AgentEventTypes.UsageUpdated, dto, cancellationToken);
        }
    }

    private async Task ExecuteCallsAsync(RunState state, IReadOnlyList<FunctionCallContent> calls, CancellationToken cancellationToken)
    {
        var offered = await OfferedToolsAsync(state, cancellationToken);
        var results = new List<AIContent>();
        foreach (var call in calls)
        {
            results.Add(await _steps.ExecuteAsync(state, call, offered, cancellationToken));
            if (state.Run.InvalidCalls >= state.Limits.MaxInvalidCalls)
            {
                throw new RunStopException(AgentFailureCodes.InvalidCalls, $"The model made {state.Run.InvalidCalls} invalid tool calls.");
            }
        }

        state.Transcript.Add(new ChatMessage(ChatRole.Tool, results));
    }

    /// <summary>Records the end exactly once, even when the stores fail (each part is attempted independently).</summary>
    private async Task EndAsync(RunState state, RunOutcome outcome)
    {
        var ended = outcome.Apply(state.Run, _services.Time.GetUtcNow());
        await GuardAsync(state, () => SaveAsync(state, ended, CancellationToken.None));
        await GuardAsync(state, () => RunLoader.ReleaseConversationAsync(_services, state, CancellationToken.None));
        await GuardAsync(state, () => _events.PublishAsync(state.RunId, outcome.EventType, outcome.Payload(state.RunId), CancellationToken.None));
    }

    private async Task GuardAsync(RunState state, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            RunLog.EndFailed(_services.Logger, state.RunId, exception);
        }
    }

    private async Task SaveAsync(RunState state, AgentRun run, CancellationToken cancellationToken)
    {
        state.Run = run;
        await _services.Runs.UpdateRunAsync(run, cancellationToken);
    }
}
