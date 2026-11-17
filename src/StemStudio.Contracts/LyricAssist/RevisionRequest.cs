namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of undo and restore (schema <c>RevisionRequest</c>).</summary>
public sealed record RevisionRequest
{
    /// <summary>The current revision as the editor knows it.</summary>
    public required int ExpectedRevision { get; init; }
}
