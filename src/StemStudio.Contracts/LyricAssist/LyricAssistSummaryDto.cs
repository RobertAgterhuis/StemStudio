using StemStudio.Contracts.Agent;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>An assist without its suggestions (schema <c>LyricAssistSummary</c>; list of <c>GET /api/lyrics/{lyricsId}/assists</c>).</summary>
public record LyricAssistSummaryDto
{
    /// <summary>Assist id.</summary>
    public required Guid AssistId { get; init; }

    /// <summary>The sheet.</summary>
    public required Guid LyricsId { get; init; }

    /// <summary>The revision checked.</summary>
    public required int SheetRevision { get; init; }

    /// <summary>What it does.</summary>
    public required AssistKindDto Kind { get; init; }

    /// <summary>State.</summary>
    public required AssistStatusDto Status { get; init; }

    /// <summary>Sheet language.</summary>
    public required AssistLanguageDto Language { get; init; }

    /// <summary>Sensitivity used.</summary>
    public required SensitivityDto Sensitivity { get; init; }

    /// <summary>Layers (at most 3).</summary>
    public required IReadOnlyList<LayerInfoDto> Layers { get; init; }

    /// <summary>Model backend; null when no model layer ran (never <c>none</c>).</summary>
    public AgentBackendDto? Backend { get; init; }

    /// <summary>Model name.</summary>
    public string? Model { get; init; }

    /// <summary>True while the CPU fallback model was used.</summary>
    public required bool UsingFallback { get; init; }

    /// <summary>Model blocks in total.</summary>
    public required int BlocksTotal { get; init; }

    /// <summary>Blocks done.</summary>
    public required int BlocksDone { get; init; }

    /// <summary>Blocks skipped.</summary>
    public required int BlocksSkipped { get; init; }

    /// <summary>Shown suggestions per category.</summary>
    public required CategoryCountsDto Counts { get; init; }

    /// <summary>Discarded model items per reason.</summary>
    public required DiscardedCountsDto Discarded { get; init; }

    /// <summary>Claude token usage; null for local models.</summary>
    public TokenUsageDto? Usage { get; init; }

    /// <summary>Who started it.</summary>
    public required AssistOriginDto Origin { get; init; }

    /// <summary>The agent run that started it.</summary>
    public Guid? AgentRunId { get; init; }

    /// <summary>Failure, when failed.</summary>
    public AssistFailureDto? Failure { get; init; }

    /// <summary>Estimated duration.</summary>
    public double? EstimatedSeconds { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Start time.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>End time.</summary>
    public DateTimeOffset? EndedAt { get; init; }
}
