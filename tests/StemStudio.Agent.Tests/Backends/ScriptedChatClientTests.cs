using System.Text.Json;
using Microsoft.Extensions.AI;
using StemStudio.Agent.Backends.Fakes;

namespace StemStudio.Agent.Tests.Backends;

/// <summary>Skeleton tests (architect, 011 Phase 0 T004): the fake IChatClient every agent test builds on (research R-10).</summary>
public sealed class ScriptedChatClientTests
{
    private static readonly string SchemaPath = Path.Combine(AppContext.BaseDirectory, "Contract", "agent-script.schema.json");

    [Fact]
    public async Task Streaming_PlaysTextThenCallsThenUsage()
    {
        using var client = ScriptedChatClient.FromJson(SchemaExample());

        var updates = await client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Mute the drums")], cancellationToken: TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);

        var call = updates.SelectMany(u => u.Contents).OfType<FunctionCallContent>().Single();
        (call.Name, call.CallId).ShouldBe(("search_library", "call-1-1"));
        ((JsonElement)call.Arguments!["query"]!).GetString().ShouldBe("hero");
        updates.ShouldAllBe(u => u.Role == ChatRole.Assistant && u.ModelId == ScriptedChatClient.ModelId);
        client.RemainingTurns.ShouldBe(3);
    }

    [Fact]
    public async Task NonStreaming_ReturnsTheTurnWithUsage()
    {
        using var client = new ScriptedChatClient([new ScriptedTurn { Text = "Done.", Usage = new ScriptedUsage { InputTokens = 7, OutputTokens = 2, CachedInputTokens = 5 } }]);
        var options = new ChatOptions { ToolMode = ChatToolMode.Auto };

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options, TestContext.Current.CancellationToken);

        response.Text.ShouldBe("Done.");
        (response.Usage!.InputTokenCount, response.Usage.OutputTokenCount, response.Usage.CachedInputTokenCount, response.Usage.TotalTokenCount).ShouldBe((7L, 2L, 5L, 9L));
        response.Messages.Single().Contents.OfType<UsageContent>().ShouldHaveSingleItem();
        client.Requests.Single().Options.ShouldBeSameAs(options);
        client.Requests.Single().Messages.Single().Text.ShouldBe("hi");
    }

    [Fact]
    public async Task ParallelCalls_GetDistinctIdsAndMissingArgumentsStayNull()
    {
        using var client = new ScriptedChatClient([new ScriptedTurn { Calls = [new ScriptedCall { Name = "mixer_reset" }, new ScriptedCall { Name = "x", Arguments = JsonDocument.Parse("[1]").RootElement }] }]);

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "reset")], cancellationToken: TestContext.Current.CancellationToken);

        var calls = response.Messages.Single().Contents.OfType<FunctionCallContent>().ToList();
        calls.Select(c => c.CallId).ShouldBe(["call-1-1", "call-1-2"]);
        calls.ShouldAllBe(c => c.Arguments == null);
        response.Usage.ShouldBeNull();
        response.Text.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExhaustedScript_FailsTheRequest()
    {
        using var client = new ScriptedChatClient([]);

        var error = await Should.ThrowAsync<InvalidOperationException>(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: TestContext.Current.CancellationToken));

        error.Message.ShouldBe("The agent script has no turn 1.");
    }

    [Fact]
    public void CancelledToken_ThrowsBeforeConsumingATurn()
    {
        using var client = new ScriptedChatClient([new ScriptedTurn { Text = "x" }]);

        Should.Throw<OperationCanceledException>(() => client.GetStreamingResponseAsync([], cancellationToken: new CancellationToken(canceled: true)));
        client.RemainingTurns.ShouldBe(1);
        Should.Throw<ArgumentNullException>(() => client.GetStreamingResponseAsync(null!));
        Should.Throw<ArgumentNullException>(() => new ScriptedChatClient(null!));
    }

    [Theory]
    [InlineData("null", "The agent script is empty.")]
    [InlineData("""{"version":2,"turns":[]}""", "Unsupported agent script version 2.")]
    public void FromJson_RejectsUnusableScripts(string json, string message)
    {
        Should.Throw<JsonException>(() => ScriptedChatClient.FromJson(json)).Message.ShouldBe(message);
    }

    [Fact]
    public void GetService_ExposesMetadataAndItself()
    {
        using var client = new ScriptedChatClient([]);

        client.GetService<ChatClientMetadata>()!.DefaultModelId.ShouldBe(ScriptedChatClient.ModelId);
        client.GetService<ScriptedChatClient>().ShouldBeSameAs(client);
        client.GetService(typeof(string)).ShouldBeNull();
        client.GetService(typeof(ChatClientMetadata), "key").ShouldBeNull();
        Should.Throw<ArgumentNullException>(() => client.GetService(null!));
    }

    [Fact]
    public void Schema_RequiresVersionAndTurns()
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(SchemaPath));

        schema.RootElement.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ShouldBe(["version", "turns"]);
        schema.RootElement.GetProperty("$defs").GetProperty("turn").GetProperty("properties").EnumerateObject().Select(p => p.Name).ShouldBe(["text", "calls", "usage"]);
    }

    private static string SchemaExample()
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(SchemaPath));
        return schema.RootElement.GetProperty("examples")[0].GetRawText();
    }
}
