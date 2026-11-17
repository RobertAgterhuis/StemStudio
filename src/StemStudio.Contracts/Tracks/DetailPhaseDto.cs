using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Tracks;

/// <summary>Detail phase of a track (backend contract 1.2.0+, data-model.md 3.1 and 4.1 of feature 002).</summary>
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

    /// <summary>Where the detail steps ran (1.3.0); null before the first parent step, for cached results and older phases.</summary>
    public JobDeviceDto? Device { get; init; }

    /// <summary>Last change of the phase; null when the phase was never requested.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// When the detail stems were removed by a clean-up or left out of a restored backup (1.12.0, feature 013); null otherwise.
    /// While set, the stem names stay in <see cref="Parents"/>, their URLs answer 404 and "Split further" is accepted for this
    /// completed phase. Omitted from JSON when null, so phases written before 1.12.0 serialize unchanged.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? FilesRemovedAt { get; init; }
}
