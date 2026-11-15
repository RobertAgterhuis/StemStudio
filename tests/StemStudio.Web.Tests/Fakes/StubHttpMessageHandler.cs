using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>A captured request: method, path+query, content type, body text and multipart details.</summary>
public sealed record CapturedRequest(
    HttpMethod Method,
    string PathAndQuery,
    string? ContentType,
    string? Body,
    IReadOnlyList<CapturedPart> Parts);

/// <summary>One multipart part: its content class, form name, file name and size.</summary>
public sealed record CapturedPart(Type ContentType, string? Name, string? FileName, long Length);

/// <summary>Stub handler that answers with queued responses and records every request.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<CapturedRequest> Requests { get; } = [];

    public Exception? ThrowOnSend { get; set; }

    public StubHttpMessageHandler Respond(HttpStatusCode status, string? json = null, string mediaType = "application/json")
    {
        _responses.Enqueue(_ => Create(status, json, mediaType));
        return this;
    }

    public StubHttpMessageHandler RespondProblem(HttpStatusCode status, string json) =>
        Respond(status, json, "application/problem+json");

    public static HttpClient CreateClient(StubHttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://api.test") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        Requests.Add(await CaptureAsync(request, cancellationToken));
        return _responses.Count > 0 ? _responses.Dequeue()(request) : Create(HttpStatusCode.NotFound, null, "application/json");
    }

    private static HttpResponseMessage Create(HttpStatusCode status, string? json, string mediaType)
    {
        var response = new HttpResponseMessage(status);
        if (json is not null)
        {
            response.Content = new StringContent(json, Encoding.UTF8, mediaType);
        }

        return response;
    }

    private static async Task<CapturedRequest> CaptureAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var parts = new List<CapturedPart>();
        string? body = null;
        if (request.Content is MultipartFormDataContent multipart)
        {
            foreach (var part in multipart)
            {
                var bytes = await part.ReadAsByteArrayAsync(ct);
                var disposition = part.Headers.ContentDisposition ?? new ContentDispositionHeaderValue("form-data");
                parts.Add(new CapturedPart(part.GetType(), disposition.Name?.Trim('"'), disposition.FileName?.Trim('"'), bytes.LongLength));
            }
        }
        else if (request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync(ct);
        }

        return new CapturedRequest(request.Method, request.RequestUri!.PathAndQuery, request.Content?.Headers.ContentType?.MediaType, body, parts);
    }
}
