using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>A captured agent request: method, path+query, Accept and Last-Event-ID headers, body.</summary>
public sealed record CapturedAgentRequest(HttpMethod Method, string PathAndQuery, string? Accept, string? LastEventId, string? Body);

/// <summary>Stub handler for the agent client: queued JSON, problem or event-stream responses, transport failures and
/// streams that break after a given text (a dropped SSE connection).</summary>
public sealed class SseHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<CapturedAgentRequest> Requests { get; } = [];

    public static HttpClient CreateClient(SseHttpHandler handler) => new(handler) { BaseAddress = new Uri("http://api.test") };

    /// <summary>Reads a recorded stream from <c>Agent/Fixtures</c> in the output directory.</summary>
    public static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Agent", "Fixtures", name));

    public SseHttpHandler Json(HttpStatusCode status, string? json = null) => Enqueue(() => Create(status, json, "application/json"));

    public SseHttpHandler Problem(HttpStatusCode status, string json) => Enqueue(() => Create(status, json, "application/problem+json"));

    public SseHttpHandler Events(string text) => Enqueue(() => Create(HttpStatusCode.OK, text, "text/event-stream"));

    /// <summary>An event stream that delivers <paramref name="text"/> and then fails with an <see cref="IOException"/>.</summary>
    public SseHttpHandler Broken(string text) => Enqueue(() =>
    {
        var content = new StreamContent(new BreakingStream(Encoding.UTF8.GetBytes(text)));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    });

    public SseHttpHandler Unreachable() => Enqueue(() => throw new HttpRequestException("connection refused"));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var lastEventId = request.Headers.TryGetValues("Last-Event-ID", out var values) ? values.Single() : null;
        Requests.Add(new CapturedAgentRequest(request.Method, request.RequestUri!.PathAndQuery, request.Headers.Accept.ToString(), lastEventId, body));
        return _responses.Count > 0 ? _responses.Dequeue()() : Create(HttpStatusCode.NotFound, null, "application/json");
    }

    private SseHttpHandler Enqueue(Func<HttpResponseMessage> response)
    {
        _responses.Enqueue(response);
        return this;
    }

    private static HttpResponseMessage Create(HttpStatusCode status, string? text, string mediaType)
    {
        var response = new HttpResponseMessage(status);
        if (text is not null)
        {
            response.Content = new StringContent(text, Encoding.UTF8, mediaType);
        }

        return response;
    }

    private sealed class BreakingStream(byte[] data) : MemoryStream(data)
    {
        public override int Read(byte[] buffer, int offset, int count) => Check(base.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Check(await base.ReadAsync(buffer, cancellationToken));

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Check(await base.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));

        private static int Check(int read) => read > 0 ? read : throw new IOException("connection reset");
    }
}
