using System.Net;
using System.Text;

namespace StemStudio.Agent.Tests.Fakes;

/// <summary>An HTTP handler that answers per "METHOD /path" and records every request with its body.</summary>
public sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<string, Task<HttpResponseMessage>>> _routes = new(StringComparer.Ordinal);
    private readonly List<(string Route, string Body)> _requests = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<(string Route, string Body)> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Answers every request with this exception (unreachable host).</summary>
    public Exception? Failure { get; set; }

    /// <summary>When set, requests wait until cancelled (hanging host).</summary>
    public bool Hang { get; set; }

    public StubHttpHandler Json(string route, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[route] = _ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        return this;
    }

    public StubHttpHandler On(string route, Func<string, string> json)
    {
        _routes[route] = body => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json(body), Encoding.UTF8, "application/json") });
        return this;
    }

    public HttpClient Client(string baseUrl = "http://ollama.test:11434/") => new(this) { BaseAddress = new Uri(baseUrl) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var route = $"{request.Method} {request.RequestUri!.AbsolutePath}";
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (_gate)
        {
            _requests.Add((route, body));
        }

        if (Hang)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (Failure is { } failure)
        {
            throw failure;
        }

        return _routes.TryGetValue(route, out var answer) ? await answer(body) : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
