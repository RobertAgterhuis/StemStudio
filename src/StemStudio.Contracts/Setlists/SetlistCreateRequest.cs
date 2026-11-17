namespace StemStudio.Contracts.Setlists;

/// <summary>Body of <c>createSetlist</c> (<c>SetlistCreate</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record SetlistCreateRequest
{
    /// <summary>Name, 1..80 characters after trimming.</summary>
    public required string Name { get; init; }

    /// <summary>Note, 0..2000 characters; default empty.</summary>
    public string Note { get; init; } = "";
}
