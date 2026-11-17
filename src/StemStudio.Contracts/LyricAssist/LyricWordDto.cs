namespace StemStudio.Contracts.LyricAssist;

/// <summary>A personal word (schema <c>LyricWord</c>).</summary>
public sealed record LyricWordDto
{
    /// <summary>Word id.</summary>
    public required Guid WordId { get; init; }

    /// <summary>Language.</summary>
    public required AssistLanguageDto Language { get; init; }

    /// <summary>The word.</summary>
    public required string Word { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
