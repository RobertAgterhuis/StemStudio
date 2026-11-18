using System.Text.RegularExpressions;

namespace StemStudio.Domain.InstrumentViews;

/// <summary>
/// A chord label in Harte syntax (data-model 2.1): <c>N</c> (no chord), <c>X</c> (unknown) or
/// <c>&lt;root&gt;:&lt;quality&gt;[/&lt;bass interval&gt;]</c>, e.g. <c>A:min7/b7</c>. The edit pattern of backend 1.15.0.
/// </summary>
public static partial class HarteLabel
{
    /// <summary>Longest label (contract <c>ChordLabelMaxLength</c>).</summary>
    public const int MaxLength = 32;

    /// <summary>The label of "no chord".</summary>
    public const string NoChord = "N";

    /// <summary>The label of an unknown chord.</summary>
    public const string Unknown = "X";

    private static readonly Dictionary<char, int> Naturals = new() { ['C'] = 0, ['D'] = 2, ['E'] = 4, ['F'] = 5, ['G'] = 7, ['A'] = 9, ['B'] = 11 };
    private static readonly int[] DegreeSemitones = [0, 2, 4, 5, 7, 9, 11];

    /// <summary>Reads a label.</summary>
    /// <param name="label">The label (null or anything outside the pattern fails).</param>
    /// <param name="chord">The chord: root, quality and the bass pitch class when it is not the root.</param>
    /// <returns>True when the label matches the pattern.</returns>
    public static bool TryParse(string? label, out ChordName chord)
    {
        chord = ChordName.None;
        if (label is null || label.Length > MaxLength || Pattern().Match(label) is not { Success: true } match)
        {
            return false;
        }

        if (!match.Groups["root"].Success)
        {
            return true;
        }

        var root = PitchClass.Of(Naturals[match.Groups["root"].Value[0]] + Accidental(match.Groups["accidental"].Value));
        var bass = match.Groups["degree"].Success ? PitchClass.Of(root + Interval(match.Groups["flat"].Success, match.Groups["degree"].Value)) : (int?)null;
        chord = new ChordName(root, ChordQualities.FromHarte(match.Groups["quality"].Value), bass == root ? null : bass);
        return true;
    }

    private static int Accidental(string accidental) => accidental switch
    {
        "#" => 1,
        "b" => -1,
        _ => 0,
    };

    private static int Interval(bool flat, string degree) => DegreeSemitones[degree[0] - '1'] - (flat ? 1 : 0);

    [GeneratedRegex(@"\A(?:N|X|(?<root>[A-G])(?<accidental>#|b)?:(?<quality>maj|min|dim|aug|min6|maj6|min7|minmaj7|maj7|7|dim7|hdim7|sus2|sus4)(?:/(?<flat>b)?(?<degree>[1-7]))?)\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
