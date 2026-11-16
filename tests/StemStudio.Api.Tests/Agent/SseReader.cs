namespace StemStudio.Api.Tests.Agent;

/// <summary>One parsed server-sent event.</summary>
internal sealed record SseEvent(string? Id, string Event, string Data);

/// <summary>Reads a <c>text/event-stream</c> body: events (id, event, data) and comment lines.</summary>
internal sealed class SseReader(StreamReader reader)
{
    private readonly Queue<string> _comments = new();

    public static IReadOnlyList<SseEvent> Parse(string body)
    {
        using var reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body)));
        var sse = new SseReader(reader);
        var events = new List<SseEvent>();
        while (sse.TryReadEventAsync().GetAwaiter().GetResult() is { } next)
        {
            events.Add(next);
        }

        return events;
    }

    public async Task<SseEvent> NextEventAsync() => await TryReadEventAsync() ?? throw new EndOfStreamException("The stream ended.");

    public async Task<string> NextCommentAsync()
    {
        while (_comments.Count == 0)
        {
            var line = await ReadLineAsync() ?? throw new EndOfStreamException("The stream ended.");
            if (line.StartsWith(':'))
            {
                _comments.Enqueue(line[1..]);
            }
        }

        return _comments.Dequeue();
    }

    public async Task<bool> IsEndAsync() => await ReadLineAsync() is null;

    private async Task<SseEvent?> TryReadEventAsync()
    {
        string? id = null, type = null, data = null;
        while (await ReadLineAsync() is { } line)
        {
            if (line.Length == 0 && type is not null)
            {
                return new SseEvent(id, type, data ?? string.Empty);
            }

            if (line.StartsWith(':'))
            {
                _comments.Enqueue(line[1..]);
            }

            id = Field(line, "id: ") ?? id;
            type = Field(line, "event: ") ?? type;
            data = Field(line, "data: ") ?? data;
        }

        return null;
    }

    private static string? Field(string line, string prefix) => line.StartsWith(prefix, StringComparison.Ordinal) ? line[prefix.Length..] : null;

    private Task<string?> ReadLineAsync() => reader.ReadLineAsync(TestContext.Current.CancellationToken).AsTask();
}
