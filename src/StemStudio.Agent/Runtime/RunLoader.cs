using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>Loads a run with its conversation and stores what the run produces in the conversation.</summary>
internal static class RunLoader
{
    /// <summary>The state of a runnable run; null for unknown or terminal runs or a missing command.</summary>
    public static async Task<RunState?> LoadAsync(AgentServices services, Guid runId, CancellationToken cancellationToken)
    {
        var run = await services.Runs.GetRunAsync(runId, cancellationToken);
        if (run is null || run.IsTerminal)
        {
            return null;
        }

        var messages = await services.Conversations.GetMessagesAsync(run.ConversationId, cancellationToken);
        return messages.FirstOrDefault(message => message.Id == run.UserMessageId) is { } command
            ? State(run, command, messages, await services.Settings.GetAsync(cancellationToken))
            : null;
    }

    private static RunState State(AgentRun run, ConversationMessage command, IReadOnlyList<ConversationMessage> messages, AgentSettings settings) => new()
    {
        Run = run,
        Command = command,
        History = [.. messages.Where(message => message.Seq < command.Seq)],
        UserMessages = [.. messages.Where(message => message.Role == MessageRoleDto.User).Select(message => message.Text)],
        Limits = settings.Limits,
        Page = command.Context.Page.Trim().ToLowerInvariant(),
    };

    /// <summary>Appends the run's answer as the assistant message (at most 16,000 characters).</summary>
    public static async Task<ConversationMessage> AppendAnswerAsync(AgentServices services, RunState state, CancellationToken cancellationToken)
    {
        var messages = await services.Conversations.GetMessagesAsync(state.Run.ConversationId, cancellationToken);
        var message = Answer(services, state, messages.Count == 0 ? 1 : messages.Max(item => item.Seq) + 1);
        await services.Conversations.AppendMessageAsync(message, cancellationToken);
        return message;
    }

    private static ConversationMessage Answer(AgentServices services, RunState state, int seq)
    {
        var text = state.Answer.ToString().Trim();
        return new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = state.Run.ConversationId,
            Seq = seq,
            Role = MessageRoleDto.Assistant,
            Text = text.Length > AgentContractLimits.AssistantMessageMaxLength ? text[..AgentContractLimits.AssistantMessageMaxLength] : text,
            Context = state.Command.Context,
            RunId = state.RunId,
            CreatedAt = services.Time.GetUtcNow(),
        };
    }

    /// <summary>Clears the conversation's active run when it is this run.</summary>
    public static async Task ReleaseConversationAsync(AgentServices services, RunState state, CancellationToken cancellationToken)
    {
        var conversation = await services.Conversations.GetAsync(state.Run.ConversationId, cancellationToken);
        if (conversation is not null && conversation.ActiveRunId == state.RunId)
        {
            await services.Conversations.UpdateAsync(conversation with { ActiveRunId = null, UpdatedAt = services.Time.GetUtcNow() }, cancellationToken);
        }
    }
}
