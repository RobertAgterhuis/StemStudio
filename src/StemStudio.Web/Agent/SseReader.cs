using System.Runtime.CompilerServices;
using System.Text;

namespace StemStudio.Web.Agent;

/// <summary>One dispatched server-sent event.</summary>
/// <param name="Id">The last event id seen on the stream (kept across events), or null.</param>
/// <param name="Event">The event type (<c>message</c> when the event has no <c>event:</c> field).</param>
/// <param name="Data">The data lines joined with <c>\n</c>.</param>
internal sealed record SseMessage(string? Id, string Event, string Data);

/// <summary>
/// Parses a <c>text/event-stream</c> body (WHATWG HTML "server-sent events" format): comments (<c>: keep-alive</c>)
/// and unknown fields are ignored, an event is dispatched on a blank line, an event without data is dropped and an
/// unterminated last event is discarded.
/// </summary>
internal static class SseReader
{
    /// <summary>Reads the events of a stream until it ends.</summary>
    /// <param name="stream">The response body.</param>
    /// <param name="ct">Cancels reading.</param>
    /// <returns>The dispatched events in order.</returns>
    public static async IAsyncEnumerable<SseMessage> ReadAsync(Stream stream, [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var pending = new PendingEvent();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Length > 0)
            {
                pending.Apply(line);
            }
            else if (pending.Dispatch() is { } message)
            {
                yield return message;
            }
        }
    }

    private sealed class PendingEvent
    {
        private readonly StringBuilder _data = new();
        private string? _id;
        private string? _event;
        private bool _hasData;

        public void Apply(string line)
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon != 0)
            {
                Set(colon < 0 ? line : line[..colon], colon < 0 ? string.Empty : Value(line, colon));
            }
        }

        public SseMessage? Dispatch()
        {
            var message = _hasData ? new SseMessage(_id, _event ?? "message", _data.ToString()) : null;
            _data.Clear();
            _event = null;
            _hasData = false;
            return message;
        }

        private static string Value(string line, int colon) =>
            colon + 1 < line.Length && line[colon + 1] == ' ' ? line[(colon + 2)..] : line[(colon + 1)..];

        private void Set(string field, string value)
        {
            switch (field)
            {
                case "data":
                    _data.Append(_hasData ? "\n" : string.Empty).Append(value);
                    _hasData = true;
                    break;
                case "event":
                    _event = value;
                    break;
                case "id":
                    _id = value;
                    break;
            }
        }
    }
}
