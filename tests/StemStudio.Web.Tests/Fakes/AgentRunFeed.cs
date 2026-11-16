using System.Text.Json;
using System.Threading.Channels;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>A live run stream a test pushes events into (the fake side of <c>GET /api/agent/runs/{id}/events</c>).</summary>
public sealed class AgentRunFeed
{
    private readonly Channel<AgentEvent> _channel = Channel.CreateUnbounded<AgentEvent>();
    private int _seq;

    /// <summary>The <c>after</c> value the client asked for on the last subscription.</summary>
    public int? After { get; private set; }

    public IAsyncEnumerable<AgentEvent> Read(int? after, CancellationToken ct)
    {
        After = after;
        return _channel.Reader.ReadAllAsync(ct);
    }

    public AgentRunFeed Push(string type, object data)
    {
        _channel.Writer.TryWrite(new AgentEvent(++_seq, type, JsonSerializer.SerializeToElement(data, JsonSerializerOptions.Web)));
        return this;
    }

    public AgentRunFeed Started(AgentBackendDto backend = AgentBackendDto.Ollama, string model = "qwen3.5:9b", bool fallback = false) =>
        Push(AgentEventTypes.RunStarted, new RunStartedEventDto { RunId = AgentData.RunId, Backend = backend, Model = model, UsingFallback = fallback });

    public AgentRunFeed Text(string text) => Push(AgentEventTypes.TextDelta, new TextDeltaEventDto { Text = text });

    public AgentRunFeed Completed() => Push(AgentEventTypes.RunCompleted, new RunCompletedEventDto { RunId = AgentData.RunId, MessageId = AgentData.MessageId });

    public AgentRunFeed Failed(string message, string? fix = null, string code = AgentFailureCodes.Timeout) =>
        Push(AgentEventTypes.RunFailed, new RunFailureDto { Code = code, Message = message, Fix = fix });

    public void Fail(Exception error) => _channel.Writer.TryComplete(error);

    public void End() => _channel.Writer.TryComplete();
}
