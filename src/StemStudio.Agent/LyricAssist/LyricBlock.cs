namespace StemStudio.Agent.LyricAssist;

/// <summary>One block of the sheet reviewed by one model request (research R-05).</summary>
public sealed record LyricBlock
{
    /// <summary>0-based block index.</summary>
    public required int Index { get; init; }

    /// <summary>First line of the block (1-based, inclusive).</summary>
    public required int FirstLine { get; init; }

    /// <summary>Last line of the block (1-based, inclusive).</summary>
    public required int LastLine { get; init; }

    /// <summary>Dictionary misses inside the block left after the deterministic licence rules (research R-03 step 5).</summary>
    public IReadOnlyList<DictionaryMiss> Misses { get; init; } = [];
}
