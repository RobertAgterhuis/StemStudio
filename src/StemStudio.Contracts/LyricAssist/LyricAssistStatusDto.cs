namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of <c>GET /api/lyric-assist/status</c> (always 200).</summary>
public sealed record LyricAssistStatusDto
{
    /// <summary>Supported sheet languages.</summary>
    public required IReadOnlyList<AssistLanguageDto> Languages { get; init; }

    /// <summary>The dictionary layer.</summary>
    public required LayerInfoDto Dictionary { get; init; }

    /// <summary>The model layer.</summary>
    public required LyricAssistModelStatusDto Model { get; init; }

    /// <summary>True when the backend is <c>claude</c> and the current lyric-assistant notice is not acknowledged.</summary>
    public required bool ClaudeNoticeRequired { get; init; }

    /// <summary>Id of the running assist, if any.</summary>
    public required Guid? AssistActive { get; init; }
}
