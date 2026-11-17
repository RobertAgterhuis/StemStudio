using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of accept, undo and restore (schema <c>AcceptResult</c>).</summary>
public sealed record AcceptResultDto
{
    /// <summary>The sheet.</summary>
    public required Guid LyricsId { get; init; }

    /// <summary>New revision.</summary>
    public required int Revision { get; init; }

    /// <summary>Title after the action.</summary>
    public required string Title { get; init; }

    /// <summary>Text after the action.</summary>
    public required string Text { get; init; }

    /// <summary>Assistant state after the action.</summary>
    public required SheetAssistInfoDto Assist { get; init; }

    /// <summary>Suggestions applied.</summary>
    public required IReadOnlyList<Guid> Applied { get; init; }

    /// <summary>Requested suggestions skipped because their anchor was stale.</summary>
    public required IReadOnlyList<Guid> Stale { get; init; }

    /// <summary>Suggestions reopened by an undo or restore; omitted for an accept.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Guid>? Reopened { get; init; }
}
