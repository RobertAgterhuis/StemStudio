using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>
/// <c>POST /api/agent/conversations/{id}/messages</c> (FR-001, FR-002): validates the command, stores the user message, creates
/// the queued run (one active run globally) and hands it to the executor.
/// </summary>
/// <param name="conversations">Conversation store.</param>
/// <param name="runs">Run store.</param>
/// <param name="factory">Builds the run from the settings.</param>
/// <param name="executor">Run executor.</param>
public sealed class AgentMessageService(IConversationStore conversations, IRunStore runs, AgentRunFactory factory, AgentRunExecutor executor)
{
    private const int DefaultTitleLength = 60;

    /// <summary>Sends a command.</summary>
    /// <param name="conversationId">Conversation id.</param>
    /// <param name="request">Text and page context.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 422.</returns>
    public async Task<IResult> SendAsync(Guid conversationId, SendAgentMessageRequest? request, CancellationToken cancellationToken)
    {
        if (await conversations.GetAsync(conversationId, cancellationToken) is not { } conversation)
        {
            return AgentConversationService.ConversationNotFound();
        }

        if (Validate(request) is { } invalid)
        {
            return invalid;
        }

        if (await runs.GetActiveRunAsync(cancellationToken) is not null)
        {
            return AgentProblems.RunActive();
        }

        return await StartAsync(conversation, request!, cancellationToken);
    }

    private async Task<IResult> StartAsync(Conversation conversation, SendAgentMessageRequest request, CancellationToken cancellationToken)
    {
        var messages = await conversations.GetMessagesAsync(conversation.Id, cancellationToken);
        var (run, message) = await factory.CreateAsync(conversation.Id, request, messages.Count + 1, cancellationToken);
        try
        {
            await runs.CreateRunAsync(run, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return AgentProblems.RunActive();
        }

        await conversations.AppendMessageAsync(message, cancellationToken);
        await conversations.UpdateAsync(Touch(conversation, request.Text, run), cancellationToken);
        executor.Enqueue(run.Id);
        return TypedResults.Accepted((string?)null, new AgentMessageAcceptedDto { RunId = run.Id, MessageId = message.Id });
    }

    private static Conversation Touch(Conversation conversation, string text, AgentRun run) => conversation with
    {
        Title = conversation.Title.Length == 0 ? text.Trim()[..Math.Min(DefaultTitleLength, text.Trim().Length)] : conversation.Title,
        UpdatedAt = run.StartedAt,
        ActiveRunId = run.Id,
    };

    private static IResult? Validate(SendAgentMessageRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > AgentContractLimits.MessageMaxLength)
        {
            return AgentProblems.Unprocessable("text", "Type a command of 1 to 4,000 characters.");
        }

        var page = request.Context?.Page;
        return string.IsNullOrWhiteSpace(page) || page.Length > AgentContractLimits.PageMaxLength
            ? AgentProblems.Unprocessable("context.page", "The page context has 1 to 40 characters.")
            : null;
    }
}

/// <summary>Builds a queued run and its user message from the current agent settings.</summary>
/// <param name="settings">Settings store.</param>
/// <param name="clock">Clock.</param>
public sealed class AgentRunFactory(IAgentSettingsStore settings, TimeProvider clock)
{
    /// <summary>Model name stored for runs of backends without a model (none, fake).</summary>
    public const string ScriptedModel = "scripted";

    /// <summary>Creates the run (status queued) and the user message that starts it.</summary>
    /// <param name="conversationId">Conversation id.</param>
    /// <param name="request">The command.</param>
    /// <param name="seq">Seq of the message.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The run and the message.</returns>
    public async Task<(AgentRun Run, ConversationMessage Message)> CreateAsync(Guid conversationId, SendAgentMessageRequest request, int seq, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var current = await settings.GetAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var message = new ConversationMessage
        {
            Id = Guid.NewGuid(), ConversationId = conversationId, Seq = seq, Role = MessageRoleDto.User, Text = request.Text,
            Context = request.Context, CreatedAt = now,
        };
        var run = new AgentRun
        {
            Id = Guid.NewGuid(), ConversationId = conversationId, UserMessageId = message.Id, Status = AgentRunStatusDto.Queued,
            Backend = current.Backend, Model = ModelOf(current), Tainted = false, ModelTurns = 0, ToolCalls = 0, InvalidCalls = 0, StartedAt = now,
        };
        return (run, message with { RunId = run.Id });
    }

    /// <summary>The model the selected backend starts with.</summary>
    /// <param name="current">Settings.</param>
    /// <returns>The model name.</returns>
    public static string ModelOf(AgentSettings current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return current.Backend switch
        {
            AgentBackendDto.Ollama => current.OllamaModel,
            AgentBackendDto.Claude => Stores.AgentStoreValues.ContractName(current.ClaudeModel),
            _ => ScriptedModel,
        };
    }
}
