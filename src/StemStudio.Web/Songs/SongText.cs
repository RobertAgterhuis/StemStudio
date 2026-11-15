using System.Text.RegularExpressions;
using StemStudio.Contracts.Generation;

namespace StemStudio.Web.Songs;

/// <summary>Option lists and texts of the song page (feature 010, data-model 2.1/3.2).</summary>
public static partial class SongText
{
    private static readonly string[] Tonics = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    private static readonly Dictionary<string, string> Meters = new() { ["2"] = "2/4", ["3"] = "3/4", ["4"] = "4/4", ["6"] = "6/8" };

    /// <summary>The 24 keys the API accepts (<c>^[A-G](#|b)? (major|minor)$</c>), majors first.</summary>
    public static IReadOnlyList<string> Keys { get; } = [.. Tonics.Select(t => $"{t} major"), .. Tonics.Select(t => $"{t} minor")];

    /// <summary>The beats-per-bar values of the contract with their label, e.g. <c>3</c> = "3/4".</summary>
    public static IReadOnlyDictionary<string, string> TimeSignatures => Meters;

    /// <summary>The readable lead voice.</summary>
    /// <param name="voice">The lead voice.</param>
    /// <returns>"Female", "Male" or "Any".</returns>
    public static string LeadVoice(LeadVoiceDto voice) => voice.ToString();

    /// <summary>The label of a beats-per-bar value.</summary>
    /// <param name="beats">For example <c>4</c>.</param>
    /// <returns>For example "4/4".</returns>
    public static string TimeSignature(string beats) => Meters.GetValueOrDefault(beats, beats);

    /// <summary>Counts the sung words: section markers such as <c>[verse 2]</c> are not words.</summary>
    /// <param name="lyrics">The lyrics.</param>
    /// <returns>The number of words.</returns>
    public static int Words(string lyrics) =>
        Marker().Replace(lyrics, " ").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    [GeneratedRegex(@"\[[^\]\r\n]*\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Marker();
}
