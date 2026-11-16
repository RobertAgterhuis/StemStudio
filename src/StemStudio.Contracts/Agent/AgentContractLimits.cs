namespace StemStudio.Contracts.Agent;

/// <summary>Size and time limits of the agent API that are part of the contract (spec FR-008, FR-026).</summary>
public static class AgentContractLimits
{
    /// <summary>Maximum length of a user command.</summary>
    public const int MessageMaxLength = 4000;

    /// <summary>Maximum length of an assistant message.</summary>
    public const int AssistantMessageMaxLength = 16000;

    /// <summary>Maximum length of <see cref="PageContextDto.Page"/>.</summary>
    public const int PageMaxLength = 40;

    /// <summary>Maximum length of a conversation title or plan item title.</summary>
    public const int TitleMaxLength = 120;

    /// <summary>Default page size of the conversation list.</summary>
    public const int ConversationListDefault = 50;

    /// <summary>Maximum page size of the conversation list.</summary>
    public const int ConversationListMax = 100;

    /// <summary>Maximum size of canonical tool arguments in bytes.</summary>
    public const int ArgumentsMaxBytes = 8192;

    /// <summary>Maximum size of a tool result projection or UI result in bytes.</summary>
    public const int ResultMaxBytes = 4096;

    /// <summary>Maximum length of an error text.</summary>
    public const int ErrorMaxLength = 500;

    /// <summary>Seconds the open page has to answer a UI tool request.</summary>
    public const int UiToolTimeoutSeconds = 15;

    /// <summary>Seconds between SSE keep-alive comments.</summary>
    public const int KeepAliveSeconds = 15;

    /// <summary>Maximum request body of PUT /api/agent/settings/claude-key in bytes (1.21.0; larger bodies answer 413).</summary>
    public const int ClaudeKeyBodyBytes = 1024;

    /// <summary>Maximum length of <see cref="StoreClaudeKeyRequest.ApiKey"/> as sent (schema <c>maxLength</c>, before trimming).</summary>
    public const int ClaudeKeyRequestMaxLength = 512;
}
