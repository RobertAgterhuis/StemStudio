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
/// context budget, the page context line, the command and the run's own transcript (FR-003, FR-004).
/// </summary>
public static class TurnBuilder
{
    /// <summary>Builds the request.</summary>
    /// <param name="input">History, command, transcript and tools.</param>
    /// <param name="baseOptions">Backend options of the turn, when any.</param>
    /// <returns>The request.</returns>
    public static TurnRequest Build(TurnInput input, ChatOptions? baseOptions)
    {
        ArgumentNullException.ThrowIfNull(input);
        var messages = new List<ChatMessage> { new(ChatRole.System, SystemPrompt.Text) };
        messages.AddRange(input.History.Select(ToChat));
        messages.Add(new ChatMessage(ChatRole.System, SystemPrompt.ContextLine(input.Context)));
        messages.Add(new ChatMessage(ChatRole.User, input.Command));
        messages.AddRange(input.Transcript);
        return new TurnRequest(messages, Options(input.Tools, baseOptions));
    }

    /// <summary>The tool declaration the model sees.</summary>
    /// <param name="tool">The tool.</param>
    /// <returns>The declaration.</returns>
    public static AIFunctionDeclaration Declaration(AgentToolDescriptor tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return AIFunctionFactory.CreateDeclaration(tool.Name, tool.Description, tool.Schema.ToJsonSchema());
    }

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
    /// <summary>Earlier messages of the conversation, oldest first.</summary>
    public required IReadOnlyList<ConversationMessage> History { get; init; }

    /// <summary>The command text.</summary>
    public required string Command { get; init; }

    /// <summary>Page context.</summary>
    public required PageContextDto Context { get; init; }

    /// <summary>This run's assistant turns and tool results.</summary>
    public IReadOnlyList<ChatMessage> Transcript { get; init; } = [];

    /// <summary>The offered tools.</summary>
    public required IReadOnlyList<AgentToolDescriptor> Tools { get; init; }
}
