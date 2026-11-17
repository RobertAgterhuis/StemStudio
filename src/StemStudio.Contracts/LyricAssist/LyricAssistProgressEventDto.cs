namespace StemStudio.Contracts.LyricAssist;

/// <summary>Data of the <c>assist.progress</c> event.</summary>
public sealed record LyricAssistProgressEventDto
{
    /// <summary>Blocks done.</summary>
    public required int BlocksDone { get; init; }

    /// <summary>Blocks skipped.</summary>
    public required int BlocksSkipped { get; init; }

    /// <summary>Blocks in total.</summary>
    public required int BlocksTotal { get; init; }
}
