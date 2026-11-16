namespace StemStudio.Contracts.Generation;

/// <summary>Body of <c>POST /api/songs</c> (1.9.0); one of lyrics id or text, at most one of style id or free caption.</summary>
public sealed record SongRequest
{
    /// <summary>A saved lyrics sheet.</summary>
    public Guid? LyricsId { get; init; }

    /// <summary>Inline lyrics (normalised by the API).</summary>
    public string? LyricsText { get; init; }

    /// <summary>Song title (default the sheet title or "Untitled song").</summary>
    public string? Title { get; init; }

    /// <summary>Language code (<see cref="ContractLimits.LyricsLanguages"/>).</summary>
    public required string Language { get; init; }

    /// <summary>A style of the library.</summary>
    public Guid? StyleId { get; init; }

    /// <summary>A free description (guarded), at most 300 characters.</summary>
    public string? FreeCaption { get; init; }

    /// <summary>Lead voice.</summary>
    public required LeadVoiceDto LeadVoice { get; init; }

    /// <summary>Delivery words (guarded), at most 40 characters.</summary>
    public string? Delivery { get; init; }

    /// <summary>No vocals.</summary>
    public bool Instrumental { get; init; }

    /// <summary>Tempo 40-220.</summary>
    public int? Bpm { get; init; }

    /// <summary>Key, e.g. <c>E minor</c>.</summary>
    public string? KeyScale { get; init; }

    /// <summary><c>2</c>, <c>3</c>, <c>4</c> or <c>6</c>.</summary>
    public string? TimeSignature { get; init; }

    /// <summary>Length 10-480 s.</summary>
    public double DurationSeconds { get; init; } = 120;

    /// <summary>Number of takes, 1-2.</summary>
    public int Takes { get; init; } = 1;
}
