using System.Text.Json;
using Microsoft.Extensions.AI;
using StemStudio.Agent.Model;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>The messages and options of one model turn.</summary>
/// <param name="Messages">Messages in order.</param>
/// <param name="Options">Options with the tool declarations.</param>
public sealed record TurnRequest(IReadOnlyList<ChatMessage> Messages, ChatOptions Options);

/// <summary>
/// Builds one model request: the stable system prompt, the offered tools (sorted by name), the newest history that fits the
/// context budget (estimated as characters / 3.5), the page context line, the command and the run's own transcript
/// (FR-003, FR-004). The system prompt and the tool list are byte-identical across turns so backends can cache the prefix.
/// </summary>
public static class TurnBuilder
{
    private const double CharactersPerToken = 3.5;

    /// <summary>Builds the request.</summary>
    /// <param name="input">History, command, transcript, tools and budget.</param>
    /// <param name="baseOptions">Backend options of the turn, when any (not modified).</param>
    /// <returns>The request.</returns>
    public static TurnRequest Build(TurnInput input, ChatOptions? baseOptions)
    {
        ArgumentNullException.ThrowIfNull(input);
        var options = Options(input.Tools, baseOptions);
        var tail = new List<ChatMessage> { new(ChatRole.System, SystemPrompt.ContextLine(input.Context)), new(ChatRole.User, input.Command) };
        tail.AddRange(input.Transcript);
        var system = new ChatMessage(ChatRole.System, SystemPrompt.Text);
        var used = EstimateTokens(system) + EstimateTools(options) + tail.Sum(EstimateTokens);
        return new TurnRequest([system, .. History(input.History, input.BudgetTokens - used), .. tail], options);
    }

    /// <summary>Estimated tokens of a text (characters / 3.5, rounded up).</summary>
    /// <param name="text">Text.</param>
    /// <returns>Tokens.</returns>
    public static int EstimateTokens(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return (int)Math.Ceiling(text.Length / CharactersPerToken);
    }

    /// <summary>The tool declaration the model sees.</summary>
    /// <param name="tool">The tool.</param>
    /// <returns>The declaration.</returns>
    public static AIFunctionDeclaration Declaration(AgentToolDescriptor tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return AIFunctionFactory.CreateDeclaration(tool.Name, tool.Description, tool.Schema.ToJsonSchema());
    }

    private static List<ChatMessage> History(IReadOnlyList<ConversationMessage> history, int budget)
    {
        var kept = new List<ChatMessage>();
        for (var index = history.Count - 1; index >= 0; index--)
        {
            var message = ToChat(history[index]);
            budget -= EstimateTokens(message);
            if (budget < 0)
            {
                break;
            }

            kept.Insert(0, message);
        }

        return kept;
    }

    private static int EstimateTokens(ChatMessage message) => EstimateTokens(message.Text) + message.Contents.Sum(content => content switch
    {
        FunctionCallContent call => EstimateTokens(call.Name + JsonSerializer.Serialize(call.Arguments)),
        FunctionResultContent result => EstimateTokens(result.Result?.ToString() ?? string.Empty),
        _ => 0,
    });

    private static int EstimateTools(ChatOptions options) =>
        options.Tools!.OfType<AIFunctionDeclaration>().Sum(tool => EstimateTokens(tool.Name + tool.Description + tool.JsonSchema.GetRawText()));

    private static ChatOptions Options(IReadOnlyList<AgentToolDescriptor> tools, ChatOptions? baseOptions)
    {
        var options = baseOptions?.Clone() ?? new ChatOptions();
        options.Tools = [.. tools.OrderBy(tool => tool.Name, StringComparer.Ordinal).Select(Declaration)];
        options.ToolMode = ChatToolMode.Auto;
        return options;
    }

    private static ChatMessage ToChat(ConversationMessage message) =>
        new(message.Role == MessageRoleDto.User ? ChatRole.User : ChatRole.Assistant, message.Text);
}

/// <summary>What one model request is built from.</summary>
public sealed record TurnInput
{
    /// <summary>Default context budget: Ollama num_ctx 16,384 minus 4,096 for the answer (research F-12).</summary>
    public const int DefaultBudgetTokens = 12_288;

    /// <summary>Earlier messages of the conversation, oldest first.</summary>
    public required IReadOnlyList<ConversationMessage> History { get; init; }

    /// <summary>The command text.</summary>
    public required string Command { get; init; }

    /// <summary>Page context.</summary>
    public required PageContextDto Context { get; init; }

    /// <summary>This run's assistant turns and tool results (always kept).</summary>
    public IReadOnlyList<ChatMessage> Transcript { get; init; } = [];

    /// <summary>The offered tools.</summary>
    public required IReadOnlyList<AgentToolDescriptor> Tools { get; init; }

    /// <summary>Token budget of the request (estimated as characters / 3.5).</summary>
    public int BudgetTokens { get; init; } = DefaultBudgetTokens;
}
