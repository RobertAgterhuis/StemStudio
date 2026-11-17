using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using StemStudio.Agent.Backends.Fakes;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;
using static StemStudio.Agent.Tests.Fakes.TestTools;

namespace StemStudio.Agent.Tests.Backends;

/// <summary>CTO decision for 023 Q4 (qa T060): <c>{{context.lyricsId}}</c> and <c>{{result.&lt;tool&gt;.ids:&lt;category&gt;}}</c>.</summary>
public sealed class ScriptPlaceholderTests
{
    private const string ListResult = """
        {"revision":3,"counts":{"grammar":2,"spelling":1},"items":[
          {"suggestionId":"11111111-1111-1111-1111-111111111111","category":"grammar","type":"grammar.verb-form","severity":"error","line":2,"status":"open","source":"model"},
          {"suggestionId":"22222222-2222-2222-2222-222222222222","category":"spelling","type":"spelling.typo","severity":"error","line":3,"status":"open","source":"dictionary"},
          {"suggestionId":"33333333-3333-3333-3333-333333333333","category":"grammar","type":"grammar.agreement","severity":"error","line":5,"status":"open","source":"model"}
        ]}
        """;

    private const string ResultScript = """
        {"version":1,"turns":[
          {"calls":[{"name":"list_lyric_suggestions","arguments":{"lyricsId":"{{context.lyricsId}}"}}]},
          {"calls":[{"name":"apply","arguments":{"lyricsId":"{{context.lyricsId}}","suggestionIds":"{{result.list_lyric_suggestions.ids:grammar}}","note":"x {{result.list_lyric_suggestions.ids:grammar}}"}}]},
          {"text":"Done."}
        ]}
        """;

    private static AgentToolDescriptor List() => Server("list_lyric_suggestions", _ => ToolResult.Of(JsonNode.Parse(ListResult)!), schema: ToolSchema.Of(Param.Text("lyricsId", 0, 40)));

    private static AgentToolDescriptor Apply() => Server("apply", _ => ToolResult.Of(new JsonObject()), schema: ToolSchema.Of(
        Param.Text("lyricsId", 0, 40), Param.List("suggestionIds", Param.Uuid("id"), 0, 50), Param.Text("note", 0, 200)));

    [Fact]
    public async Task LyricsIdAndResultIds_AreSubstituted()
    {
        var lyricsId = Guid.NewGuid();
        var harness = new RunnerHarness(new ScriptedChatClient([]), [List(), Apply()]);
        harness.UseSession(new ScriptedChatSession(ResultScript));
        harness.SetCommandContext(new PageContextDto { Page = "lyrics", LyricsId = lyricsId });

        var run = await harness.ExecuteAsync();

        run.Status.ShouldBe(AgentRunStatusDto.Completed);
        harness.Runs.Steps[0].Arguments.ShouldBe($$"""{"lyricsId":"{{lyricsId}}"}""");
        const string Untouched = "{{result.list_lyric_suggestions.ids:grammar}}";
        harness.Runs.Steps[1].Arguments.ShouldBe($"{{\"lyricsId\":\"{lyricsId}\",\"note\":\"x {Untouched}\",\"suggestionIds\":[\"11111111-1111-1111-1111-111111111111\",\"33333333-3333-3333-3333-333333333333\"]}}");
    }

    [Fact]
    public void ACategoryWithoutItems_GivesAnEmptyArray()
    {
        var resolved = Resolve("""{"ids":"{{result.list_lyric_suggestions.ids:flow}}"}""", Messages(ListResult));

        resolved.GetProperty("ids").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void TheMostRecentResultWins_AndNestedValuesAreResolved()
    {
        var older = """{"items":[{"suggestionId":"44444444-4444-4444-4444-444444444444","category":"grammar"}]}""";
        var messages = Messages(older).Concat(Messages(ListResult, "call-2")).ToList();

        var resolved = Resolve("""{"wrap":{"ids":"{{result.list_lyric_suggestions.ids:spelling}}"},"list":["{{result.list_lyric_suggestions.ids:spelling}}"]}""", messages);

        resolved.GetProperty("wrap").GetProperty("ids")[0].GetString().ShouldBe("22222222-2222-2222-2222-222222222222");
        resolved.GetProperty("list")[0][0].GetString().ShouldBe("22222222-2222-2222-2222-222222222222");
    }

    [Fact]
    public void ResultsThatAreNotJsonOrHaveNoItems_GiveAnEmptyArray()
    {
        Resolve("""{"ids":"{{result.list_lyric_suggestions.ids:grammar}}"}""", Messages("not json")).GetProperty("ids").GetArrayLength().ShouldBe(0);
        Resolve("""{"ids":"{{result.list_lyric_suggestions.ids:grammar}}"}""", Messages("""{"ok":false}""")).GetProperty("ids").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void NoEarlierCall_IsAClearScriptError()
    {
        var error = Should.Throw<InvalidOperationException>(() => Resolve("""{"ids":"{{result.list_lyric_suggestions.ids:grammar}}"}""", []));

        error.Message.ShouldContain("before any list_lyric_suggestions call");
    }

    [Fact]
    public void AnUnsupportedTool_IsAClearScriptError()
    {
        var error = Should.Throw<InvalidOperationException>(() => Resolve("""{"ids":"{{result.list_mixes.ids:grammar}}"}""", Messages(ListResult)));

        error.Message.ShouldContain("not a supported list tool");
    }

    [Fact]
    public void ArgumentsWithoutResultPlaceholders_AreReturnedAsIs()
    {
        var arguments = JsonDocument.Parse("""{"a":"{{context.trackId}}"}""").RootElement;

        ScriptResultPlaceholders.Resolve(arguments, []).ShouldBe(arguments);
        ScriptResultPlaceholders.Resolve(null, []).ShouldBeNull();
        ScriptResultPlaceholders.IsPlaceholder("{{result.list_lyric_suggestions.ids:grammar}}").ShouldBeTrue();
        ScriptResultPlaceholders.IsPlaceholder(" {{result.list_lyric_suggestions.ids:grammar}}").ShouldBeFalse();
    }

    [Fact]
    public async Task TheClient_FailsTheTurnWhenTheScriptUsesAResultTooEarly()
    {
        var client = ScriptedChatClient.FromJson("""{"version":1,"turns":[{"calls":[{"name":"apply","arguments":{"suggestionIds":"{{result.list_lyric_suggestions.ids:grammar}}"}}]}]}""");

        await Should.ThrowAsync<InvalidOperationException>(() => client.GetResponseAsync([], cancellationToken: TestContext.Current.CancellationToken));
    }

    private static JsonElement Resolve(string arguments, IReadOnlyList<ChatMessage> messages) =>
        ScriptResultPlaceholders.Resolve(JsonDocument.Parse(arguments).RootElement, messages)!.Value;

    private static List<ChatMessage> Messages(string result, string callId = "call-1") =>
    [
        new(ChatRole.Assistant, [new FunctionCallContent(callId, "list_lyric_suggestions")]),
        new(ChatRole.Tool, [new FunctionResultContent(callId, result)]),
    ];
}
