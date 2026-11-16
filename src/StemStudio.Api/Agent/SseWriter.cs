using System.Globalization;
using System.Text;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Stores;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>
/// Writes the event stream of a run (contracts/DELTA.md section 3): the stored events after the client's position, then live
/// events, a <c>: keep-alive</c> comment every 15 s, and the end of the stream after the terminal event.
/// </summary>
/// <param name="hub">Event log and live subscriptions.</param>
/// <param name="runs">Run store (to end streams of runs that ended without a terminal event).</param>
/// <param name="clock">Clock of the keep-alive timer.</param>
public sealed class SseWriter(RunEventHub hub, IRunStore runs, TimeProvider clock)
{
    private static readonly TimeSpan KeepAlive = TimeSpan.FromSeconds(AgentContractLimits.KeepAliveSeconds);

    /// <summary>Streams the events of a run to the response until the terminal event or the client disconnects.</summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="runId">Run id.</param>
    /// <param name="after">Last Seq the client has.</param>
    /// <param name="cancellationToken">Request aborted.</param>
    /// <returns>A task.</returns>
    public async Task StreamAsync(HttpResponse response, Guid runId, int after, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache";
        using var subscription = hub.Subscribe(runId);
        try
        {
            var last = await ReplayAsync(response, runId, after, cancellationToken);
            if (last is not null)
            {
                await LiveAsync(response, subscription, last.Value, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client went away.
        }
    }

    /// <summary>Formats one event.</summary>
    /// <param name="runEvent">The event.</param>
    /// <returns>The SSE text block.</returns>
    public static string Format(RunEvent runEvent)
    {
        ArgumentNullException.ThrowIfNull(runEvent);
        return string.Create(CultureInfo.InvariantCulture, $"id: {runEvent.Seq}\nevent: {runEvent.Type}\ndata: {runEvent.Payload}\n\n");
    }

    private async Task<int?> ReplayAsync(HttpResponse response, Guid runId, int after, CancellationToken cancellationToken)
    {
        var last = after;
        foreach (var stored in await hub.ReadAfterAsync(runId, after, cancellationToken))
        {
            await WriteAsync(response, Format(stored), cancellationToken);
            last = stored.Seq;
            if (AgentEventTypes.IsTerminal(stored.Type))
            {
                return null;
            }
        }

        await response.Body.FlushAsync(cancellationToken);
        var run = await runs.GetRunAsync(runId, cancellationToken);
        return run is { IsTerminal: true } ? null : last;
    }

    private async Task LiveAsync(HttpResponse response, RunEventSubscription subscription, int last, CancellationToken cancellationToken)
    {
        Task<RunEvent>? pending = null;
        while (true)
        {
            pending ??= subscription.Events.ReadAsync(cancellationToken).AsTask();
            using var tick = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var keepAlive = Task.Delay(KeepAlive, clock, tick.Token);
            if (await Task.WhenAny(pending, keepAlive) == keepAlive)
            {
                await WriteAsync(response, ": keep-alive\n\n", cancellationToken);
                continue;
            }

            await tick.CancelAsync();
            var live = await pending;
            pending = null;
            if (live.Seq > last && await WriteLiveAsync(response, live, cancellationToken))
            {
                return;
            }

            last = Math.Max(last, live.Seq);
        }
    }

    private static async Task<bool> WriteLiveAsync(HttpResponse response, RunEvent live, CancellationToken cancellationToken)
    {
        await WriteAsync(response, Format(live), cancellationToken);
        return AgentEventTypes.IsTerminal(live.Type);
    }

    private static async Task WriteAsync(HttpResponse response, string text, CancellationToken cancellationToken)
    {
        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
