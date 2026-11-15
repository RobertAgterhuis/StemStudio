using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace StemStudio.Web.Audio;

/// <summary>One word of the karaoke banner (<c>KaraokeWord</c> in types.ts, engine contract 2.1.0).</summary>
/// <param name="Text">Text (1..100 characters); the engine sets it with <c>textContent</c>.</param>
/// <param name="StartMs">The ball lands on the word at this time (ms from the track start).</param>
/// <param name="EndMs">End of the word, &gt; <paramref name="StartMs"/>.</param>
/// <remarks><c>ToString()</c> never prints the text (006 FR-010).</remarks>
public sealed record KaraokeWord(string Text, int StartMs, int EndMs)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"StartMs = {StartMs}, EndMs = {EndMs}");
        return true;
    }
}

/// <summary>One line of the karaoke banner (<c>KaraokeLine</c> in types.ts).</summary>
/// <param name="StartMs">Start of the line.</param>
/// <param name="EndMs">End of the line, &gt; <paramref name="StartMs"/>.</param>
/// <param name="Words">Words in order (1..40).</param>
/// <remarks><c>ToString()</c> never prints the text; it counts the words.</remarks>
public sealed record KaraokeLine(int StartMs, int EndMs, IReadOnlyList<KaraokeWord> Words)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"StartMs = {StartMs}, EndMs = {EndMs}, Words = {Words.Count}");
        return true;
    }
}

/// <summary>Timed lyrics to draw (<c>KaraokeLyrics</c> in types.ts): the render subset of the backend lyrics.</summary>
/// <param name="Lines">Lines in order of their start (at most 400).</param>
/// <remarks><c>ToString()</c> never prints the text; it counts the lines.</remarks>
public sealed record KaraokeLyrics(IReadOnlyList<KaraokeLine> Lines)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Lines = {Lines.Count}");
        return true;
    }
}

/// <summary>Options of <c>attachKaraoke</c> (<c>KaraokeOptions</c> in types.ts).</summary>
/// <param name="OffsetMs">User offset -5000..5000 ms; positive = the lyrics come later.</param>
/// <param name="ReducedMotion">
/// True when the app's own motion setting asks for reduced motion; null leaves it out, so the engine follows the operating
/// system preference live.
/// </param>
public sealed record KaraokeOptions(
    int OffsetMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? ReducedMotion = null);
