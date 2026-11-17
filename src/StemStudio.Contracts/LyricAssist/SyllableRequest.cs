namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of <c>POST /api/lyric-assist/syllables</c> (schema <c>SyllableRequest</c>); nothing is stored.</summary>
public sealed record SyllableRequest
{
    /// <summary>Language.</summary>
    public required AssistLanguageDto Language { get; init; }

    /// <summary>Raw editor text (normalised like a 010 save).</summary>
    public required string Text { get; init; }

    /// <summary>Optional rhythm target.</summary>
    public RhythmTargetDto? RhythmTarget { get; init; }
}
