using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>
/// An in-memory generator service behind the real <c>GeneratorClient</c> (stub <see cref="HttpMessageHandler"/>): jobs
/// are the generator contract examples with their own ids; tests change a job's JSON to drive the sync service.
/// </summary>
internal sealed class FakeGenerator
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, JsonObject> _jobs = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Id, int Index), byte[]> _takes = [];
    private int _counter;

    public FakeGenerator() => Handler = new StubHttpMessageHandler(request => Task.FromResult(Respond(request)));

    public StubHttpMessageHandler Handler { get; }

    /// <summary>The <c>/health</c> example (<c>ReadyHealth</c>, <c>NoGpuHealth</c>, ...).</summary>
    public string HealthExample { get; set; } = "ReadyHealth";

    /// <summary>When set and returning a response, it answers instead of the fake (fault injection).</summary>
    public Func<HttpRequestMessage, HttpResponseMessage?>? Override { get; set; }

    public List<CreatedJob> Created { get; } = [];

    public List<string> Deleted { get; } = [];

    public List<string> Cancelled { get; } = [];

    public int HealthCalls { get; private set; }

    public JsonObject Job(string id)
    {
        lock (_lock)
        {
            return _jobs[id];
        }
    }

    public bool HasJob(string id)
    {
        lock (_lock)
        {
            return _jobs.ContainsKey(id);
        }
    }

    /// <summary>Replaces a job with an example (status, takes, error), keeping its id.</summary>
    public void SetJob(string id, string example, Action<JsonObject>? change = null)
    {
        var job = Example(example, id);
        change?.Invoke(job);
        lock (_lock)
        {
            _jobs[id] = job;
        }
    }

    /// <summary>Completes a job with takes of the given bytes (hashes and sizes match the bytes).</summary>
    public void Complete(string id, params byte[][] takes)
    {
        SetJob(id, "CompletedRestyle", job =>
        {
            var list = job["takes"]!.AsArray();
            while (list.Count > takes.Length)
            {
                list.RemoveAt(list.Count - 1);
            }

            for (var i = 0; i < takes.Length; i++)
            {
                list[i]!["sha256"] = Convert.ToHexStringLower(SHA256.HashData(takes[i]));
                list[i]!["bytes"] = takes[i].Length;
            }
        });
        lock (_lock)
        {
            for (var i = 0; i < takes.Length; i++)
            {
                _takes[(id, i + 1)] = takes[i];
            }
        }
    }

    public void Forget(string id)
    {
        lock (_lock)
        {
            _jobs.Remove(id);
        }
    }

    private HttpResponseMessage Respond(HttpRequestMessage request) => Override?.Invoke(request) ?? Route(request);

    private HttpResponseMessage Route(HttpRequestMessage request)
    {
        var segments = request.RequestUri!.AbsolutePath.Trim('/').Split('/');
        return (request.Method.Method, segments) switch
        {
            ("GET", ["health"]) => Health(),
            ("POST", ["v1", "generations"]) => Create(request),
            ("GET", ["v1", "generations", var id]) => Get(id),
            ("POST", ["v1", "generations", var id, "cancel"]) => Cancel(id),
            ("GET", ["v1", "generations", var id, "takes", var index]) => Take(id, int.Parse(index, CultureInfo.InvariantCulture)),
            ("DELETE", ["v1", "generations", var id]) => Delete(id),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private HttpResponseMessage Health()
    {
        HealthCalls++;
        return GeneratorExamples.Response(HttpStatusCode.OK, HealthExample);
    }

    private HttpResponseMessage Create(HttpRequestMessage request)
    {
        string id;
        lock (_lock)
        {
            id = (++_counter).ToString("x32", CultureInfo.InvariantCulture);
        }

        var created = request.Content is MultipartFormDataContent multipart ? FromMultipart(id, multipart.ToList()) : FromJson(id, request.Content!);
        Created.Add(created);
        SetJob(id, created.Params["kind"]!.GetValue<string>() == "song" ? "QueuedSong" : "QueuedRestyle");
        return Get(id, HttpStatusCode.Accepted);
    }

    private static CreatedJob FromMultipart(string id, List<HttpContent> parts)
    {
        var files = parts.Skip(1).Select(part => (part.Headers.ContentDisposition!.FileName!.Trim('"'), part.ReadAsByteArrayAsync().GetAwaiter().GetResult())).ToList();
        var names = parts.Skip(1).Select(part => part.Headers.ContentDisposition!.Name!.Trim('"')).ToList();
        return new CreatedJob(id, JsonNode.Parse(parts[0].ReadAsStringAsync().GetAwaiter().GetResult())!.AsObject(), files) { PartNames = names };
    }

    private static CreatedJob FromJson(string id, HttpContent content) =>
        new(id, JsonNode.Parse(content.ReadAsStringAsync().GetAwaiter().GetResult())!.AsObject(), []);

    private HttpResponseMessage Get(string id, HttpStatusCode status = HttpStatusCode.OK)
    {
        lock (_lock)
        {
            return _jobs.TryGetValue(id, out var job) ? StubHttpMessageHandler.Json(status, job.ToJsonString()) : GeneratorExamples.Problem(HttpStatusCode.NotFound, null, "Unknown generation.");
        }
    }

    private HttpResponseMessage Cancel(string id)
    {
        Cancelled.Add(id);
        if (HasJob(id) && Job(id)["status"]!.GetValue<string>() is not ("completed" or "failed"))
        {
            SetJob(id, "CancelledRestyle");
        }

        return Get(id);
    }

    private HttpResponseMessage Take(string id, int index)
    {
        lock (_lock)
        {
            return _takes.TryGetValue((id, index), out var bytes)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : GeneratorExamples.Problem(HttpStatusCode.NotFound, null, "No such take.");
        }
    }

    private HttpResponseMessage Delete(string id)
    {
        Deleted.Add(id);
        lock (_lock)
        {
            return new HttpResponseMessage(_jobs.Remove(id) ? HttpStatusCode.NoContent : HttpStatusCode.NotFound);
        }
    }

    private static JsonObject Example(string name, string id)
    {
        var job = JsonNode.Parse(GeneratorExamples.Read(name))!.AsObject();
        job["generationId"] = id;
        return job;
    }
}

internal sealed record CreatedJob(string Id, JsonObject Params, IReadOnlyList<(string FileName, byte[] Bytes)> Stems)
{
    public IReadOnlyList<string> PartNames { get; init; } = [];
}

/// <summary>Seeds 1000, 2000, ... so generator requests are predictable.</summary>
internal sealed class SequenceSeeds : StemStudio.Api.Generations.ISeedSource
{
    private int _next;

    public int NewSeed() => Interlocked.Increment(ref _next) * 1000;
}
