using System.Diagnostics;
using System.Globalization;
using System.Text;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// The SSE stream of an assist (data-model 10, same framing as 011): the stored events after the client's position (<c>after</c> or
/// <c>Last-Event-ID</c>), then new ones as they are stored, a <c>: keep-alive</c> comment every 15 s, closed after the terminal event.
/// </summary>
/// <param name="assists">Assist repository (events are read without tracking).</param>
/// <param name="options">Poll and keep-alive intervals.</param>
public sealed class LyricAssistEventWriter(ILyricAssistRepository assists, LyricAssistHostOptions options)
{
    /// <summary>Streams until the terminal event or the client disconnects.</summary>
    /// <param name="response">The response.</param>
    /// <param name="assistId">Assist id.</param>
    /// <param name="after">Last sequence number the client has.</param>
    /// <param name="cancellationToken">Request aborted.</param>
    /// <returns>A task.</returns>
    public async Task StreamAsync(HttpResponse response, Guid assistId, int after, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache";
        try
        {
            await response.StartAsync(cancellationToken);
            await LoopAsync(response, assistId, after, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client went away.
        }
    }

    /// <summary>Formats one event.</summary>
    /// <param name="assistEvent">The event.</param>
    /// <returns>The SSE block.</returns>
    public static string Format(AssistEvent assistEvent)
    {
        ArgumentNullException.ThrowIfNull(assistEvent);
        return string.Create(CultureInfo.InvariantCulture, $"id: {assistEvent.Seq}\nevent: {assistEvent.Type}\ndata: {assistEvent.Data}\n\n");
    }

    private async Task LoopAsync(HttpResponse response, Guid assistId, int last, CancellationToken cancellationToken)
    {
        var poll = TimeSpan.FromMilliseconds(Math.Max(options.StreamPollMilliseconds, 10));
        var timer = new StreamTimer();
        var first = true;
        while (true)
        {
            var (written, ended) = await WriteStoredAsync(response, assistId, last, cancellationToken);
            if (ended)
            {
                return;
            }

            if (first && written == last && await EndedAsync(assistId, cancellationToken))
            {
                await WriteStoredAsync(response, assistId, last, cancellationToken);
                return;
            }

            first = false;

            if (written > last)
            {
                (last, timer.LastEvent, timer.LastWrite) = (written, Stopwatch.GetTimestamp(), Stopwatch.GetTimestamp());
            }
            else if (!await IdleTickAsync(response, assistId, timer, cancellationToken))
            {
                return;
            }

            await Task.Delay(poll, cancellationToken);
        }
    }

    // A reconnect after the terminal event (EventSource sends its Last-Event-ID) ends at once instead of polling until the idle timeout.
    private async Task<bool> EndedAsync(Guid assistId, CancellationToken cancellationToken) =>
        await assists.FindAsync(assistId, cancellationToken) is not { } assist || assist.Status.IsTerminal();

    // The stored events after the position; the last written seq and whether a terminal event was written.
    private async Task<(int Last, bool Ended)> WriteStoredAsync(HttpResponse response, Guid assistId, int last, CancellationToken cancellationToken)
    {
        foreach (var assistEvent in await assists.EventsAsync(assistId, last, cancellationToken))
        {
            await WriteAsync(response, Format(assistEvent), cancellationToken);
            last = assistEvent.Seq;
            if (LyricAssistEventTypes.IsTerminal(assistEvent.Type))
            {
                return (last, true);
            }
        }

        return (last, false);
    }

    // Closes an idle stream (security review) or a stream of a deleted assist; writes the keep-alive comment when due.
    private async Task<bool> IdleTickAsync(HttpResponse response, Guid assistId, StreamTimer timer, CancellationToken cancellationToken)
    {
        if (Stopwatch.GetElapsedTime(timer.LastEvent) >= TimeSpan.FromSeconds(options.StreamIdleSeconds))
        {
            return false;
        }

        if (Stopwatch.GetElapsedTime(timer.LastWrite) < TimeSpan.FromSeconds(options.KeepAliveSeconds))
        {
            return true;
        }

        if (await assists.FindAsync(assistId, cancellationToken) is null)
        {
            return false;
        }

        await WriteAsync(response, ": keep-alive\n\n", cancellationToken);
        timer.LastWrite = Stopwatch.GetTimestamp();
        return true;
    }

    private static async Task WriteAsync(HttpResponse response, string text, CancellationToken cancellationToken)
    {
        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private sealed class StreamTimer
    {
        public long LastEvent { get; set; } = Stopwatch.GetTimestamp();

        public long LastWrite { get; set; } = Stopwatch.GetTimestamp();
    }
}
