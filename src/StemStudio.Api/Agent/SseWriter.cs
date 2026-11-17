using System.Globalization;
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
        try
        {
            await StreamEventsAsync(response, runId, after, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client went away.
        }
    }

    // Subscribed before the replay, so no event published in between is lost (live events up to the replayed Seq are skipped).
    private async Task StreamEventsAsync(HttpResponse response, Guid runId, int after, CancellationToken cancellationToken)
    {
        using var subscription = hub.Subscribe(runId);
        await response.StartAsync(cancellationToken);
        if (await ReplayAsync(response, runId, after, cancellationToken) is { } last)
        {
            await LiveAsync(response, subscription, last, cancellationToken);
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
        if (await WriteStoredAsync(response, runId, after, cancellationToken) is not { } last)
        {
            return null;
        }

        await response.Body.FlushAsync(cancellationToken);
        var run = await runs.GetRunAsync(runId, cancellationToken);
        return run is { IsTerminal: true } ? null : last;
    }

    // The last written Seq, or null when a terminal event was written.
    private async Task<int?> WriteStoredAsync(HttpResponse response, Guid runId, int after, CancellationToken cancellationToken)
    {
        var last = after;
        foreach (var stored in await hub.ReadAfterAsync(runId, after, cancellationToken))
        {
            await SseText.WriteAsync(response, Format(stored), cancellationToken);
            last = stored.Seq;
            if (AgentEventTypes.IsTerminal(stored.Type))
            {
                return null;
            }
        }

        return last;
    }

    private async Task LiveAsync(HttpResponse response, RunEventSubscription subscription, int last, CancellationToken cancellationToken)
    {
        var events = new LiveEvents(subscription.Events, clock);
        var ended = false;
        while (!ended)
        {
            var live = await events.NextAsync(cancellationToken);
            ended = await WriteNextAsync(response, live, last, cancellationToken);
            last = Math.Max(last, live?.Seq ?? last);
        }
    }

    // A keep-alive comment when no event arrived in time; events up to the replayed Seq are skipped. True after the terminal event.
    private static async Task<bool> WriteNextAsync(HttpResponse response, RunEvent? live, int last, CancellationToken cancellationToken)
    {
        if (live is null)
        {
            await SseText.WriteAsync(response, SseText.KeepAlive, cancellationToken);
            return false;
        }

        return live.Seq > last && await WriteLiveAsync(response, live, cancellationToken);
    }

    private static async Task<bool> WriteLiveAsync(HttpResponse response, RunEvent live, CancellationToken cancellationToken)
    {
        await SseText.WriteAsync(response, Format(live), cancellationToken);
        return AgentEventTypes.IsTerminal(live.Type);
    }
}
