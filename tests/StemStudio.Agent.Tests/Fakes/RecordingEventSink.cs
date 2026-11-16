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

    /// <summary>Called synchronously for every published event (simulates a page that answers immediately).</summary>
    public Action<RunEvent>? OnPublish { get; set; }

    public IReadOnlyList<string> Types => [.. Events.Select(item => item.Type)];

    public Task<RunEvent> PublishAsync(Guid runId, string type, string payload, CancellationToken cancellationToken)
    {
        JsonDocument.Parse(payload).Dispose();
        var (stored, ready) = Store(runId, type, payload);
        OnPublish?.Invoke(stored);
        ready.ForEach(signal => signal.TrySetResult(stored));
        return Task.FromResult(stored);
    }

    private (RunEvent Stored, List<TaskCompletionSource<RunEvent>> Ready) Store(Guid runId, string type, string payload)
    {
        lock (_gate)
        {
            var stored = new RunEvent { RunId = runId, Seq = _events.Count(item => item.RunId == runId) + 1, Type = type, Payload = payload, CreatedAt = DateTimeOffset.UnixEpoch };
            _events.Add(stored);
            List<TaskCompletionSource<RunEvent>> ready = [.. _waiters.Where(waiter => waiter.Type == type).Select(waiter => waiter.Signal)];
            _waiters.RemoveAll(waiter => waiter.Type == type);
            return (stored, ready);
        }
    }

    /// <summary>Completes when the <paramref name="count"/>-th event of the type was published.</summary>
    public async Task<RunEvent> WaitForAsync(string type, int count = 1)
    {
        while (true)
        {
            var (found, signal) = FindOrWait(type, count);
            if (found is not null)
            {
                return found;
            }

            await signal!.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private (RunEvent? Found, TaskCompletionSource<RunEvent>? Signal) FindOrWait(string type, int count)
    {
        lock (_gate)
        {
            var matches = _events.Where(item => item.Type == type).ToList();
            if (matches.Count >= count)
            {
                return (matches[count - 1], null);
            }

            var signal = new TaskCompletionSource<RunEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((type, signal));
            return (null, signal);
        }
    }

    public JsonElement Payload(string type, int index = 0) => JsonDocument.Parse(Events.Where(item => item.Type == type).ElementAt(index).Payload).RootElement;
}
