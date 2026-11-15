namespace StemStudio.Domain.Tracks.Detail;

/// <summary>Parent and detail stem names of the detail separation, in display order (data-model.md 1).</summary>
public static class DetailCatalog
{
    /// <summary>The residual detail stem; always the last stem of a split parent.</summary>
    public const string Rest = "rest";

    /// <summary>Maximum detail stems of one split parent, including <see cref="Rest"/>.</summary>
    public const int MaxStemsPerParent = 10;

    /// <summary>Standard stems that can be split, in standard display order (bass is never split).</summary>
    public static readonly IReadOnlyList<string> Parents = ["vocals", "drums", "guitar", "piano", "other"];

    private static readonly string[] Instruments =
        ["strings", "brass", "woodwinds", "keys", "synth", "acoustic-guitar", "electric-guitar", "percussion", Rest];

    private static readonly Dictionary<string, IReadOnlyList<string>> Allowed = new(StringComparer.Ordinal)
    {
        ["vocals"] = ["lead", "backing", Rest],
        ["drums"] = ["kick", "snare", "toms", "hihat", "cymbals", Rest],
        ["guitar"] = Instruments,
        ["piano"] = Instruments,
        ["other"] = Instruments,
    };

    /// <summary>Allowed detail stems of a parent in display order, <see cref="Rest"/> last.</summary>
    /// <param name="parent">Standard stem name.</param>
    /// <returns>The names; empty for bass and unknown names.</returns>
    public static IReadOnlyList<string> AllowedFor(string parent) => Allowed.GetValueOrDefault(parent) ?? [];

    /// <summary>True for a stem that can be split.</summary>
    /// <param name="name">Stem name, or null.</param>
    /// <returns>True for one of <see cref="Parents"/>.</returns>
    public static bool IsParent(string? name) => name is not null && Allowed.ContainsKey(name);

    /// <summary>True when the detail stem may exist under the parent.</summary>
    /// <param name="parent">Parent stem name.</param>
    /// <param name="name">Detail stem name.</param>
    /// <returns>True when allowed.</returns>
    public static bool IsAllowed(string parent, string name) => AllowedFor(parent).Contains(name, StringComparer.Ordinal);

    /// <summary>Display position of a detail stem within its parent's group.</summary>
    /// <param name="parent">Parent stem name.</param>
    /// <param name="name">Detail stem name.</param>
    /// <returns>The index, or -1 when not allowed.</returns>
    public static int Order(string parent, string name) => AllowedFor(parent).ToList().IndexOf(name);
}
