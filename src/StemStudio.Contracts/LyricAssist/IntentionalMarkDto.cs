namespace StemStudio.Contracts.LyricAssist;

/// <summary>An intentional mark (schema <c>IntentionalMark</c>).</summary>
public sealed record IntentionalMarkDto
{
    /// <summary>Mark id.</summary>
    public required Guid MarkId { get; init; }

    /// <summary>Scope.</summary>
    public required IntentionalScopeDto Scope { get; init; }

    /// <summary>Marked text (normalised).</summary>
    public required string Text { get; init; }

    /// <summary>Optional note.</summary>
    public string? Note { get; init; }

    /// <summary>True when the marked text currently occurs in the sheet.</summary>
    public required bool InText { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
