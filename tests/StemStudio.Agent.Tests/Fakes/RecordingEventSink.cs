using System.Text.Json;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;

namespace StemStudio.Agent.Tests.Fakes;

/// <summary>Records every published event with a per-run Seq; can signal when an event type arrives.</summary>
public sealed class RecordingEventSink : IRunEventSink
{
    private readonly List<RunEvent> _events = [];
    private readonly Lock _gate = new();
    private readonly List<(string Type, TaskCompletionSource<RunEvent> Signal)> _waiters = [];

    public IReadOnlyList<RunEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    public IReadOnlyList<string> Types => [.. Events.Select(item => item.Type)];

    public Task<RunEvent> PublishAsync(Guid runId, string type, string payload, CancellationToken cancellationToken)
    {
        JsonDocument.Parse(payload).Dispose();
        RunEvent stored;
        List<TaskCompletionSource<RunEvent>> ready;
        lock (_gate)
        {
            stored = new RunEvent { RunId = runId, Seq = _events.Count(item => item.RunId == runId) + 1, Type = type, Payload = payload, CreatedAt = DateTimeOffset.UnixEpoch };
            _events.Add(stored);
            ready = [.. _waiters.Where(waiter => waiter.Type == type).Select(waiter => waiter.Signal)];
            _waiters.RemoveAll(waiter => waiter.Type == type);
        }

        ready.ForEach(signal => signal.TrySetResult(stored));
        return Task.FromResult(stored);
    }

    /// <summary>Completes when the <paramref name="count"/>-th event of the type was published.</summary>
    public async Task<RunEvent> WaitForAsync(string type, int count = 1)
    {
        while (true)
        {
            TaskCompletionSource<RunEvent> signal;
            lock (_gate)
            {
                var matches = _events.Where(item => item.Type == type).ToList();
                if (matches.Count >= count)
                {
                    return matches[count - 1];
                }

                signal = new TaskCompletionSource<RunEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add((type, signal));
            }

            await signal.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    public JsonElement Payload(string type, int index = 0) => JsonDocument.Parse(Events.Where(item => item.Type == type).ElementAt(index).Payload).RootElement;
}
