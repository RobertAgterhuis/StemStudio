namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of <c>GET /api/lyric-assist/rhymes</c> (schema <c>RhymeResult</c>); local data only.</summary>
public sealed record RhymeResultDto
{
    /// <summary>The requested word.</summary>
    public required string Word { get; init; }

    /// <summary>Language.</summary>
    public required AssistLanguageDto Language { get; init; }

    /// <summary>Rhymes (at most 60).</summary>
    public required IReadOnlyList<RhymeDto> Rhymes { get; init; }

    /// <summary>Data source, e.g. <c>CMU Pronouncing Dictionary</c>.</summary>
    public required string Source { get; init; }
}
