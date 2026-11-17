namespace StemStudio.Contracts.LibraryCare;

/// <summary>An item a restore did not restore or renamed (<c>SkippedItem</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record SkippedItemDto
{
    /// <summary>Category or entity kind of the item.</summary>
    public required string Category { get; init; }

    /// <summary>Item id.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string? Name { get; init; }

    /// <summary>Why it was skipped.</summary>
    public required SkippedItemReasonDto Reason { get; init; }

    /// <summary>New name when the item was renamed instead (merge).</summary>
    public string? RenamedTo { get; init; }
}
