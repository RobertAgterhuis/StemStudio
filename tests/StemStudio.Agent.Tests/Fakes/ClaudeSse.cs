using Microsoft.Extensions.AI;
using StemStudio.Agent.Runtime;

namespace StemStudio.Agent.Tests.Fakes;

/// <summary>Minimal Anthropic Messages SSE answers for the stub handler, and a helper that sends one turn.</summary>
public static class ClaudeSse
{
    /// <summary>A streamed text answer.</summary>
    /// <param name="text">Answer text.</param>
    /// <returns>The event stream.</returns>
    public static string Text(string text) => string.Join("\n\n",
        Event("message_start", """{"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","model":"claude-opus-5-5","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":10,"output_tokens":1}}}"""),
        Event("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}"""),
        Event("content_block_delta", $$$"""{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"{{{text}}}"}}"""),
        Event("content_block_stop", """{"type":"content_block_stop","index":0}"""),
        Event("message_delta", """{"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":2}}"""),
        Event("message_stop", """{"type":"message_stop"}""")) + "\n\n";

    /// <summary>Sends one user message through the turn's client and reads the whole stream.</summary>
    /// <param name="turn">The turn.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The answer text.</returns>
    public static async Task<string> SendAsync(ChatTurn turn, CancellationToken cancellationToken)
    {
        var updates = await turn.Client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")], turn.Options, cancellationToken).ToListAsync(cancellationToken);
        return updates.ToChatResponse().Text;
    }

    private static string Event(string name, string data) => $"event: {name}\ndata: {data}";
}
