using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>One sheet revision (schema <c>LyricsRevisionSummary</c>, Expert list).</summary>
public sealed record LyricsRevisionSummaryDto
{
    /// <summary>Revision number.</summary>
    public required int Revision { get; init; }

    /// <summary>Why it exists.</summary>
    public required RevisionCauseDto Cause { get; init; }

    /// <summary>The accept action that produced it.</summary>
    public Guid? AcceptGroupId { get; init; }

    /// <summary>Lines changed against the previous revision; omitted when unknown.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ChangedLines { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
