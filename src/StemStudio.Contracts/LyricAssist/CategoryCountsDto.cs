namespace StemStudio.Contracts.LyricAssist;

/// <summary>Shown suggestions per category (schema <c>CategoryCounts</c>).</summary>
public sealed record CategoryCountsDto
{
    /// <summary>No suggestions.</summary>
    public static CategoryCountsDto None { get; } = new();

    /// <summary>Spelling.</summary>
    public int Spelling { get; init; }

    /// <summary>Grammar.</summary>
    public int Grammar { get; init; }

    /// <summary>Flow.</summary>
    public int Flow { get; init; }

    /// <summary>Poetic licence.</summary>
    public int Licence { get; init; }

    /// <summary>Stylistic devices.</summary>
    public int Device { get; init; }

    /// <summary>Alternative lines.</summary>
    public int Alternative { get; init; }

    /// <summary>Continuations.</summary>
    public int Continuation { get; init; }

    /// <summary>Title ideas.</summary>
    public int Title { get; init; }
}
