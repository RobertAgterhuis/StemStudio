namespace StemStudio.Contracts.Library;

/// <summary>One level of a library info state (backend-api 1.22.0 <c>LibraryInfoLevel</c>).</summary>
public sealed record LibraryInfoLevelDto
{
    /// <summary>The name the user set at this scope (track or item); null = automatic.</summary>
    public string? Value { get; init; }

    /// <summary>The name used for filing now.</summary>
    public required string Effective { get; init; }

    /// <summary>Where <see cref="Effective"/> comes from.</summary>
    public required PlacementSourceDto Source { get; init; }

    /// <summary>The name automatic filing would use at this scope.</summary>
    public required string Automatic { get; init; }
}
