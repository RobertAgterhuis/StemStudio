using System.Globalization;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>Conversation endpoints of backend 1.10.0: create, list and get (rename and delete follow in US5).</summary>
/// <param name="conversations">Conversation store.</param>
/// <param name="queries">Read models.</param>
/// <param name="clock">Clock.</param>
public sealed class AgentConversationService(IConversationStore conversations, AgentQueries queries, TimeProvider clock)
{
    /// <summary>Creates a conversation; without a title it is named after its first message.</summary>
    /// <param name="request">Optional title.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>201 or 400.</returns>
    public async Task<IResult> CreateAsync(CreateConversationRequest? request, CancellationToken cancellationToken)
    {
        var title = request?.Title?.Trim() ?? string.Empty;
        if (title.Length > AgentContractLimits.TitleMaxLength)
        {
            return ApiProblems.Validation("title", "The title has at most 120 characters.");
        }

        var now = clock.GetUtcNow();
        var conversation = new Conversation { Id = Guid.NewGuid(), Title = title, CreatedAt = now, UpdatedAt = now };
        await conversations.CreateAsync(conversation, cancellationToken);
        return TypedResults.Created(ApiRoutes.AgentConversation(conversation.Id), new ConversationDto
        {
            ConversationId = conversation.Id, Title = title, CreatedAt = now, UpdatedAt = now,
        });
    }

    /// <summary>Lists conversations newest first.</summary>
    /// <param name="limit">Page size text (1-100, default 50).</param>
    /// <param name="before">Paging cut-off text (date-time).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 400.</returns>
    public async Task<IResult> ListAsync(string? limit, string? before, CancellationToken cancellationToken)
    {
        if (!TryLimit(limit, out var size))
        {
            return ApiProblems.Validation("limit", "limit must be a whole number from 1 to 100.");
        }

        if (!TryBefore(before, out var cutoff))
        {
            return ApiProblems.Validation("before", "before must be an ISO 8601 date-time.");
        }

        var page = await conversations.ListAsync(size, cutoff, cancellationToken);
        var statuses = await queries.LastRunStatusesAsync([.. page.Select(c => c.Id)], cancellationToken);
        return TypedResults.Ok(page.Select(c => AgentDtoMapping.ToSummary(c, statuses.TryGetValue(c.Id, out var s) ? s : null)).ToList());
    }

    /// <summary>A conversation with its messages and run summaries.</summary>
    /// <param name="conversationId">Conversation id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (await conversations.GetAsync(conversationId, cancellationToken) is not { } conversation)
        {
            return ConversationNotFound();
        }

        var messages = await conversations.GetMessagesAsync(conversationId, cancellationToken);
        var runs = await queries.RunsOfAsync(conversationId, cancellationToken);
        var summary = AgentDtoMapping.ToSummary(conversation, runs.Count == 0 ? null : runs[^1].Status);
        return TypedResults.Ok(new ConversationDto
        {
            ConversationId = summary.ConversationId, Title = summary.Title, CreatedAt = summary.CreatedAt, UpdatedAt = summary.UpdatedAt,
            LastRunStatus = summary.LastRunStatus, Messages = [.. messages.Select(AgentDtoMapping.ToDto)], Runs = [.. runs.Select(AgentDtoMapping.ToSummary)],
        });
    }

    /// <summary>404 for an unknown conversation.</summary>
    /// <returns>The result.</returns>
    public static IResult ConversationNotFound() => ApiProblems.NotFound("The conversation does not exist.");

    private static bool TryLimit(string? text, out int limit)
    {
        limit = AgentContractLimits.ConversationListDefault;
        return text is null || (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit is >= 1 and <= AgentContractLimits.ConversationListMax);
    }

    private static bool TryBefore(string? text, out DateTimeOffset? before)
    {
        before = null;
        if (text is null)
        {
            return true;
        }

        var valid = DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed);
        before = valid ? parsed : null;
        return valid;
    }
}
