using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;
using StemStudio.Persistence.Agent;
using static StemStudio.Api.Agent.Stores.AgentStoreValues;

namespace StemStudio.Api.Agent.Stores;

/// <summary>Maps conversations and messages between the agent records and the persistence rows.</summary>
internal static class ConversationMapping
{
    /// <summary>Copies a conversation onto a row.</summary>
    /// <param name="entity">Target row.</param>
    /// <param name="conversation">Source record.</param>
    /// <returns>The row.</returns>
    public static ConversationEntity Apply(ConversationEntity entity, Conversation conversation)
    {
        entity.Id = conversation.Id;
        entity.Title = conversation.Title;
        entity.CreatedAt = Utc(conversation.CreatedAt);
        entity.UpdatedAt = Utc(conversation.UpdatedAt);
        entity.ActiveRunId = conversation.ActiveRunId;
        return entity;
    }

    /// <summary>Reads a conversation row.</summary>
    /// <param name="entity">The row.</param>
    /// <returns>The record.</returns>
    public static Conversation ToRecord(ConversationEntity entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        CreatedAt = Offset(entity.CreatedAt),
        UpdatedAt = Offset(entity.UpdatedAt),
        ActiveRunId = entity.ActiveRunId,
    };

    /// <summary>Creates a message row.</summary>
    /// <param name="message">The record.</param>
    /// <returns>The row.</returns>
    public static ConversationMessageEntity ToEntity(ConversationMessage message) => new()
    {
        Id = message.Id,
        ConversationId = message.ConversationId,
        Seq = message.Seq,
        Role = message.Role.ToString(),
        Text = message.Text,
        Page = message.Context.Page,
        TrackId = message.Context.TrackId,
        MixId = message.Context.MixId,
        RunId = message.RunId,
        CreatedAt = Utc(message.CreatedAt),
    };

    /// <summary>Reads a message row.</summary>
    /// <param name="entity">The row.</param>
    /// <returns>The record.</returns>
    public static ConversationMessage ToRecord(ConversationMessageEntity entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        Seq = entity.Seq,
        Role = Parse<MessageRoleDto>(entity.Role),
        Text = entity.Text,
        Context = new PageContextDto { Page = entity.Page, TrackId = entity.TrackId, MixId = entity.MixId },
        RunId = entity.RunId,
        CreatedAt = Offset(entity.CreatedAt),
    };
}
