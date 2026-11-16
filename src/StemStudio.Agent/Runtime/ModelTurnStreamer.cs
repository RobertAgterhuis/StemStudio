using System.Text;
using Microsoft.Extensions.AI;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>
/// Streams one model turn: text deltas are published coalesced per 100 ms, the whole turn is bounded by the per-turn
/// timeout (FR-011), and backend failures become run failures with readable messages.
/// </summary>
/// <param name="events">Event publisher.</param>
/// <param name="time">Clock.</param>
internal sealed class ModelTurnStreamer(RunEvents events, TimeProvider time)
{
    private static readonly TimeSpan CoalesceInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>Streams the request and returns the complete response.</summary>
    public async Task<ChatResponse> StreamAsync(Guid runId, ChatTurn turn, TurnRequest request, TurnLimit limit)
    {
        try
        {
            return await ReadAsync(runId, turn, request, limit.Token);
        }
        catch (OperationCanceledException) when (limit.TimedOut)
        {
            throw new RunStopException(AgentFailureCodes.Timeout, $"The model did not answer within {limit.Seconds} seconds.");
        }
        catch (HttpRequestException exception)
        {
            throw new RunStopException(AgentFailureCodes.BackendUnavailable, "The language model could not be reached: " + exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            throw new RunStopException(AgentFailureCodes.Internal, "The language model failed: " + exception.Message);
        }
    }

    private async Task<ChatResponse> ReadAsync(Guid runId, ChatTurn turn, TurnRequest request, CancellationToken cancellationToken)
    {
        var updates = new List<ChatResponseUpdate>();
        var pending = new StringBuilder();
        var flushedAt = time.GetTimestamp();
        await foreach (var update in turn.Client.GetStreamingResponseAsync(request.Messages, request.Options, cancellationToken))
        {
            updates.Add(update);
            pending.Append(update.Text);
            if (pending.Length > 0 && time.GetElapsedTime(flushedAt) >= CoalesceInterval)
            {
                await FlushAsync(runId, pending, cancellationToken);
                flushedAt = time.GetTimestamp();
            }
        }

        await FlushAsync(runId, pending, cancellationToken);
        return updates.ToChatResponse();
    }

    private async Task FlushAsync(Guid runId, StringBuilder pending, CancellationToken cancellationToken)
    {
        if (pending.Length > 0)
        {
            await events.PublishAsync(runId, AgentEventTypes.TextDelta, new TextDeltaEventDto { Text = pending.ToString() }, cancellationToken);
            pending.Clear();
        }
    }
}

/// <summary>The per-turn timeout linked to the run's cancellation.</summary>
internal sealed class TurnLimit : IDisposable
{
    private readonly CancellationTokenSource _timeout;
    private readonly CancellationTokenSource _linked;

    public TurnLimit(int seconds, TimeProvider time, CancellationToken cancellationToken)
    {
        Seconds = seconds;
        _timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds), time);
        _linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeout.Token);
    }

    /// <summary>Timeout in seconds.</summary>
    public int Seconds { get; }

    /// <summary>Token cancelled by the timeout or the run.</summary>
    public CancellationToken Token => _linked.Token;

    /// <summary>True when the timeout fired.</summary>
    public bool TimedOut => _timeout.IsCancellationRequested;

    /// <inheritdoc />
    public void Dispose()
    {
        _linked.Dispose();
        _timeout.Dispose();
    }
}
