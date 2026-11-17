namespace StemStudio.Contracts.LyricAssist;

/// <summary>Assistant state of a sheet (<c>LyricsSheet.assist</c>, schema <c>SheetAssistInfo</c>).</summary>
public sealed record SheetAssistInfoDto
{
    /// <summary>State of a sheet before any assistant use (also every sheet created before 1.23.0).</summary>
    public static SheetAssistInfoDto Default { get; } = new();

    /// <summary>Sensitivity of checks.</summary>
    public SensitivityDto Sensitivity { get; init; } = SensitivityDto.Balanced;

    /// <summary>Rhythm target, if set.</summary>
    public RhythmTargetDto? RhythmTarget { get; init; }

    /// <summary>Open suggestions.</summary>
    public int OpenSuggestions { get; init; }

    /// <summary>Lines from accepted assistant text (at most 400).</summary>
    public IReadOnlyList<AssistedLineDto> AssistedLines { get; init; } = [];

    /// <summary>Intentional marks of the sheet.</summary>
    public int IntentionalMarkCount { get; init; }

    /// <summary>True when the newest revision is an accept action.</summary>
    public bool CanUndo { get; init; }

    /// <summary>The own-work override, if confirmed.</summary>
    public OwnWorkOverrideDto? OwnWorkOverride { get; init; }
}
