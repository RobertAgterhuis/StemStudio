namespace StemStudio.Contracts.Tracks;

/// <summary>Detail phase of a track (backend contract 1.2.0, data-model.md 3.1 and 4.1).</summary>
public sealed record DetailPhaseDto
{
    /// <summary>The phase of a track that never requested detail separation.</summary>
    public static DetailPhaseDto None { get; } = new() { Status = DetailPhaseStatusDto.None, Progress = 0, Parents = [] };

    /// <summary>Current status.</summary>
    public required DetailPhaseStatusDto Status { get; init; }

    /// <summary>Human-readable stage, e.g. "Splitting drums (1 of 3)" (&lt;= 200 characters).</summary>
    public string? Stage { get; init; }

    /// <summary>Parent stem being split while running, see <see cref="DetailNames.Parents"/>.</summary>
    public string? CurrentParent { get; init; }

    /// <summary>Progress 0..100; never decreases within one detail job.</summary>
    public required int Progress { get; init; }

    /// <summary>Failure reason; set iff <see cref="Status"/> is failed (&lt;= 1000 characters).</summary>
    public string? Error { get; init; }

    /// <summary>One outcome per standard stem once completed (standard display order); empty otherwise.</summary>
    public required IReadOnlyList<ParentOutcomeDto> Parents { get; init; }

    /// <summary>Last change of the phase; null when the phase was never requested.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}
