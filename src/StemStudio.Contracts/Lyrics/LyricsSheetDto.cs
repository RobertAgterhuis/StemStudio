namespace StemStudio.Contracts.Lyrics;

/// <summary>Own lyrics, normalised (1.9.0). Not the synced lyrics of a track (<see cref="LyricsDto"/>).</summary>
public sealed record LyricsSheetDto
{
    /// <summary>Sheet id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Title.</summary>
    public required string Title { get; init; }

    /// <summary>Language code.</summary>
    public required string Language { get; init; }

    /// <summary>Normalised text.</summary>
    public required string Text { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last change (UTC).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Revision (1.23.0, feature 023): +1 on every save, accept, undo and restore; sheets created before 1.23.0 are 1.</summary>
    public int Revision { get; init; } = 1;

    /// <summary>Assistant state of the sheet (1.23.0, feature 023).</summary>
    public LyricAssist.SheetAssistInfoDto Assist { get; init; } = LyricAssist.SheetAssistInfoDto.Default;
}
