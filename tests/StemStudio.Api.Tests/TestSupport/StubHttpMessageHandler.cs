using System.Net;
using System.Text;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>Answers HTTP requests from a delegate and records every request with its body.</summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    public static HttpResponseMessage Json(HttpStatusCode status, string json, string mediaType = "application/json") =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, mediaType) };

    public static HttpResponseMessage Example(HttpStatusCode status, string name) =>
        Json(status, WorkerExamples.Read(name), name.StartsWith("problem", StringComparison.Ordinal) ? "application/problem+json" : "application/json");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var content = request.Content;
        var body = content is null ? null : await content.ReadAsByteArrayAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, content, body));
        return await respond(request);
    }
}

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, HttpContent? Content, byte[]? Body)
{
    public string BodyText => Body is null ? string.Empty : Encoding.UTF8.GetString(Body);
}

internal static class WorkerExamples
{
    public const string JobId = "3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c";

    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Worker", "Examples", name + ".json"));
}
