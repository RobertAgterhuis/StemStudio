using StemStudio.Agent.Backends.Fakes;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;
using static StemStudio.Agent.Tests.Fakes.TestTools;

namespace StemStudio.Agent.Tests.Backends;

/// <summary>QA lane requests for the E2E fake stack: context placeholders and a fresh script per run.</summary>
public sealed class ScriptedChatSessionTests
{
    private const string Script = """
        {"version":1,"turns":[
          {"calls":[{"name":"echo","arguments":{"value":"{{context.trackId}}|{{context.mixId}}"}}]},
          {"text":"Done."}
        ]}
        """;

    private static AgentToolDescriptor Echo() => Server("echo", invocation => ToolResult.Of(new System.Text.Json.Nodes.JsonObject { ["v"] = invocation.Text("value") }), schema: ToolSchema.Of(Param.Text("value", 1, 200)));

    [Fact]
    public async Task Placeholders_AreReplacedWithThePageContextOfTheCommand()
    {
        var trackId = Guid.NewGuid();
        var mixId = Guid.NewGuid();
        var session = new ScriptedChatSession(Script);
        var harness = new RunnerHarness(new ScriptedChatClient([]), [Echo()]);
        harness.UseSession(session);
        harness.SetCommandContext(new PageContextDto { Page = "mixer", TrackId = trackId, MixId = mixId });

        var run = await harness.ExecuteAsync();

        run.Status.ShouldBe(AgentRunStatusDto.Completed);
        harness.Runs.Steps.Single().Arguments.ShouldBe($$"""{"value":"{{trackId}}|{{mixId}}"}""");
    }

    [Fact]
    public async Task MissingContextIds_BecomeEmpty()
    {
        var session = new ScriptedChatSession(Script);
        var harness = new RunnerHarness(new ScriptedChatClient([]), [Echo()]);
        harness.UseSession(session);

        await harness.ExecuteAsync();

        harness.Runs.Steps.Single().Arguments.ShouldBe("""{"value":"|"}""");
    }

    [Fact]
    public async Task EveryRun_PlaysTheScriptFromTheFirstTurn()
    {
        var session = new ScriptedChatSession(Script);
        var first = new RunnerHarness(new ScriptedChatClient([]), [Echo()]);
        var second = new RunnerHarness(new ScriptedChatClient([]), [Echo()]);
        first.UseSession(session);
        second.UseSession(session);

        var one = await first.ExecuteAsync();
        var two = await second.ExecuteAsync();

        (one.Status, two.Status).ShouldBe((AgentRunStatusDto.Completed, AgentRunStatusDto.Completed));
        (one.ModelTurns, two.ModelTurns).ShouldBe((2, 2));
    }

    [Fact]
    public async Task Turn_ReportsTheScriptedModel()
    {
        var session = new ScriptedChatSession(Script);
        var run = new RunnerHarness(new ScriptedChatClient([]), [Echo()]).Run;

        var turn = await session.NextTurnAsync(new ChatTurnContext(run, new PageContextDto { Page = "tracks" }), TestContext.Current.CancellationToken);

        (turn.Model, turn.UsingFallback).ShouldBe((ScriptedChatClient.ModelId, false));
        turn.Client.ShouldBeOfType<ScriptedChatClient>();
    }
}
