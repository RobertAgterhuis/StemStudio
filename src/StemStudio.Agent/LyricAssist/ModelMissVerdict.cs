namespace StemStudio.Agent.LyricAssist;

/// <summary>One parsed <c>misses[]</c> entry of the model output (untrusted until validated against the block's misses).</summary>
public sealed record ModelMissVerdict
{
    /// <summary>1-based line.</summary>
    public required int Line { get; init; }

    /// <summary>The word.</summary>
    public required string Word { get; init; }

    /// <summary>Verdict.</summary>
    public required MissVerdict Verdict { get; init; }

    /// <summary>Correction of a typo (at most 40 characters).</summary>
    public string? Correction { get; init; }

    /// <summary>Licence type of a licence verdict: <c>elision</c>, <c>inversion</c>, <c>archaism</c>, <c>sound-spelling</c>, <c>slang</c>, <c>code-switch</c>.</summary>
    public string? LicenceType { get; init; }
}
