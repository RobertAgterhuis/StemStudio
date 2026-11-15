namespace StemStudio.Contracts;

/// <summary>Parent and detail stem names of the detail separation (backend contract 1.2.0, data-model.md 1).</summary>
public static class DetailNames
{
    /// <summary>The residual detail stem; always the last stem of a split parent.</summary>
    public const string Rest = "rest";

    /// <summary>Standard stems that can be split further, in standard display order (bass is never split).</summary>
    public static readonly IReadOnlyList<string> Parents = [StemNames.Vocals, StemNames.Drums, StemNames.Guitar, StemNames.Piano, StemNames.Other];

    /// <summary>Detail stems of <c>drums</c> in display order (without <see cref="Rest"/>).</summary>
    public static readonly IReadOnlyList<string> Drums = ["kick", "snare", "toms", "hihat", "cymbals"];

    /// <summary>Detail stems of <c>vocals</c> in display order (without <see cref="Rest"/>).</summary>
    public static readonly IReadOnlyList<string> Vocals = ["lead", "backing"];

    /// <summary>Instrument families of <c>guitar</c>, <c>piano</c> and <c>other</c> in display order (without <see cref="Rest"/>).</summary>
    public static readonly IReadOnlyList<string> Instruments =
        ["strings", "brass", "woodwinds", "keys", "synth", "acoustic-guitar", "electric-guitar", "percussion"];

    /// <summary>Every detail stem name (the closed set of the contract).</summary>
    public static readonly IReadOnlyList<string> All = [.. Drums, .. Vocals, .. Instruments, Rest];

    /// <summary>Allowed detail stems of a parent in display order, <see cref="Rest"/> last; empty for bass or unknown names.</summary>
    /// <param name="parent">Standard stem name.</param>
    /// <returns>The allowed detail stem names.</returns>
    public static IReadOnlyList<string> AllowedFor(string parent) => parent switch
    {
        StemNames.Drums => [.. Drums, Rest],
        StemNames.Vocals => [.. Vocals, Rest],
        StemNames.Guitar or StemNames.Piano or StemNames.Other => [.. Instruments, Rest],
        _ => [],
    };
}
