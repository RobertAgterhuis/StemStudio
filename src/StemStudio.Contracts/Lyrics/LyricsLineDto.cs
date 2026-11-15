using System.Globalization;
using System.Text;

namespace StemStudio.Contracts.Lyrics;

/// <summary>One line with its words; times are null in a plain document.</summary>
/// <param name="StartMs">Start in milliseconds from the track start.</param>
/// <param name="EndMs">End in milliseconds.</param>
/// <param name="Text">Text of the line (1..500 characters).</param>
/// <param name="Words">Words in order (1..40).</param>
/// <remarks><c>ToString()</c> never prints the text; it counts the words (FR-010).</remarks>
public sealed record LyricsLineDto(int? StartMs, int? EndMs, string Text, IReadOnlyList<LyricsWordDto> Words)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"StartMs = {StartMs}, EndMs = {EndMs}, Words = {Words.Count}");
        return true;
    }
}
