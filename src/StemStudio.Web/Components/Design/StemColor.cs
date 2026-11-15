using StemStudio.Contracts;

namespace StemStudio.Web.Components.Design;

/// <summary>
/// Stem identity (design-spec.md 2.1, data-model.md 6): one colour token per standard stem, used for its detail
/// stems too; detail stems are told apart by a pattern, never by a new hue.
/// </summary>
public static class StemColor
{
    private static readonly string[] Patterns = ["solid", "dashed", "dotted", "double"];

    /// <summary>The parent stem of a stem name, channel id (<c>drums.kick</c>) or channel key (<c>drums-kick</c>).</summary>
    /// <param name="stem">Any of the three forms.</param>
    /// <returns>The part before the first <c>.</c> or <c>-</c>.</returns>
    public static string Parent(string stem) => stem.Split('.', '-')[0];

    /// <summary>The colour variable of a stem; unknown names use <c>other</c>.</summary>
    /// <param name="stem">A stem name, channel id or channel key.</param>
    /// <returns>For example <c>var(--ss-stem-drums)</c>.</returns>
    public static string Var(string stem) => $"var(--ss-stem-{Known(Parent(stem))})";

    /// <summary>The known stem name, or <c>other</c>.</summary>
    /// <param name="stem">A stem name.</param>
    /// <returns>The stem used for colour.</returns>
    public static string Known(string stem) => StemNames.SixStem.Contains(stem) ? stem : StemNames.Other;

    /// <summary>The underline pattern of the n-th detail stem of a parent (cycles after four).</summary>
    /// <param name="detailIndex">Zero-based position of the detail stem; negative values count as 0.</param>
    /// <returns><c>solid</c>, <c>dashed</c>, <c>dotted</c> or <c>double</c>.</returns>
    public static string Pattern(int detailIndex) => Patterns[Math.Max(detailIndex, 0) % Patterns.Length];
}

/// <summary>State of the signature split visual (ui-test-ids 1.3.0 <c>data-state</c>).</summary>
public enum SplitState
{
    /// <summary>Nothing running yet (queued or not started).</summary>
    Idle,

    /// <summary>Separation in progress.</summary>
    Running,

    /// <summary>Separation completed.</summary>
    Done,
}
