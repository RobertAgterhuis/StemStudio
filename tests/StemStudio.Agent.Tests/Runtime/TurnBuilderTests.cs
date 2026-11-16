using System.Text.Json;
using Microsoft.Extensions.AI;
using StemStudio.Agent.Model;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tests.Runtime;

/// <summary>T030: context window, page context, language instruction and prompt-cache stability (FR-003, FR-004).</summary>
public sealed class TurnBuilderTests
{
    private static readonly Guid TrackId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly IReadOnlyList<AgentToolDescriptor> Tools = [.. new Core001ToolProvider().GetTools(), .. new MetaToolProvider().GetTools()];

    private static ConversationMessage Message(int seq, MessageRoleDto role, string text) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = Guid.Empty,
        Seq = seq,
        Role = role,
        Text = text,
        Context = new PageContextDto { Page = "tracks" },
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    private static TurnInput Input(IReadOnlyList<ConversationMessage>? history = null, int budget = TurnInput.DefaultBudgetTokens, IReadOnlyList<ChatMessage>? transcript = null) => new()
    {
        History = history ?? [],
        Command = "Zet de drums 3 dB harder",
        Context = new PageContextDto { Page = "mixer", TrackId = TrackId },
        Tools = Tools,
        Transcript = transcript ?? [],
        BudgetTokens = budget,
    };

    private static int Estimate(TurnRequest request) =>
        request.Messages.Sum(message => TurnBuilder.EstimateTokens(message.Text ?? string.Empty))
        + request.Options.Tools!.OfType<AIFunctionDeclaration>().Sum(tool => TurnBuilder.EstimateTokens(tool.Name + tool.Description + tool.JsonSchema.GetRawText()));

    [Fact]
    public void EstimateTokens_IsCharactersDividedBy3Point5_RoundedUp()
    {
        (TurnBuilder.EstimateTokens(string.Empty), TurnBuilder.EstimateTokens("abcdefg"), TurnBuilder.EstimateTokens("abcdefgh")).ShouldBe((0, 2, 3));
    }

    [Fact]
    public void LongHistory_KeepsTheNewestMessagesWithinTheBudget()
    {
        var history = Enumerable.Range(1, 200).Select(i => Message(i, i % 2 == 1 ? MessageRoleDto.User : MessageRoleDto.Assistant, $"message {i} " + new string('x', 200))).ToList();
        var budget = TurnBuilder.Build(Input(), null) is var empty ? Estimate(empty) + 1000 : 0;

        var request = TurnBuilder.Build(Input(history, budget), null);

        Estimate(request).ShouldBeLessThanOrEqualTo(budget);
        var kept = request.Messages.Where(message => message.Text!.StartsWith("message ", StringComparison.Ordinal)).Select(message => message.Text!.Split(' ')[1]).ToList();
        kept.ShouldNotBeEmpty();
        kept[^1].ShouldBe("200");
        kept.Count.ShouldBeLessThan(200);
        kept.Select(int.Parse).ShouldBeInOrder();
    }

    [Fact]
    public void CommandAndTranscript_AreKeptEvenWhenTheBudgetIsTiny()
    {
        var transcript = new List<ChatMessage> { new(ChatRole.Assistant, "calling"), new(ChatRole.Tool, [new FunctionResultContent("c1", "{}")]) };

        var request = TurnBuilder.Build(Input([Message(1, MessageRoleDto.User, "old")], budget: 1, transcript: transcript), null);

        request.Messages.Select(message => message.Text).ShouldNotContain("old");
        request.Messages.ShouldContain(message => message.Text == "Zet de drums 3 dB harder");
        request.Messages[^1].Role.ShouldBe(ChatRole.Tool);
    }

    [Fact]
    public void PageContext_IsAShortSystemLineBeforeTheCommand()
    {
        var request = TurnBuilder.Build(Input([Message(1, MessageRoleDto.User, "hi")]), null);

        var command = request.Messages.ToList().FindIndex(message => message.Text == "Zet de drums 3 dB harder");
        var context = request.Messages[command - 1];
        context.Role.ShouldBe(ChatRole.System);
        context.Text.ShouldBe($"Current page: mixer; open track: {TrackId}.");
    }

    [Fact]
    public void SystemPrompt_TellsTheModelToAnswerInTheUsersLanguage()
    {
        var request = TurnBuilder.Build(Input(), null);

        request.Messages[0].Role.ShouldBe(ChatRole.System);
        request.Messages[0].Text.ShouldContain("Answer in the user's language");
        request.Messages[0].Text.ShouldBe(SystemPrompt.Text);
    }

    [Fact]
    public void SystemPromptAndTools_AreByteIdenticalAcrossTurns()
    {
        var first = TurnBuilder.Build(Input(), null);
        var second = TurnBuilder.Build(Input([Message(1, MessageRoleDto.User, "earlier")], transcript: [new ChatMessage(ChatRole.Assistant, "x")]) with { Tools = [.. Tools.Reverse()] }, null);

        second.Messages[0].Text.ShouldBe(first.Messages[0].Text);
        Declarations(second).ShouldBe(Declarations(first));
        first.Options.Tools!.Select(tool => tool.Name).ShouldBeInOrder(SortDirection.Ascending, StringComparer.Ordinal);
    }

    [Fact]
    public void BaseOptions_AreKept_AndToolModeIsAuto()
    {
        var baseOptions = new ChatOptions { ModelId = "qwen3.5:9b", Temperature = 0.2f };

        var request = TurnBuilder.Build(Input(), baseOptions);

        (request.Options.ModelId, request.Options.Temperature, request.Options.ToolMode).ShouldBe(("qwen3.5:9b", 0.2f, ChatToolMode.Auto));
        baseOptions.Tools.ShouldBeNull();
    }

    private static string Declarations(TurnRequest request) =>
        string.Join("\n", request.Options.Tools!.OfType<AIFunctionDeclaration>().Select(tool => tool.Name + tool.Description + JsonSerializer.Serialize(tool.JsonSchema)));
}
