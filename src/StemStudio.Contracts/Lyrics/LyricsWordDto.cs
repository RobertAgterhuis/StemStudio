using System.Globalization;
using System.Text;

namespace StemStudio.Contracts.Lyrics;

/// <summary>One word; times (ms from the track start) and origin are null in a plain document.</summary>
/// <param name="Text">Text (1..100 characters).</param>
/// <param name="StartMs">Start in milliseconds.</param>
/// <param name="EndMs">End in milliseconds (&gt; start, at most start + 3000).</param>
/// <param name="Origin">Where the time comes from.</param>
/// <param name="LowConfidence">Aligned word with a low probability (expert view).</param>
/// <remarks><c>ToString()</c> never prints the text (FR-010).</remarks>
public sealed record LyricsWordDto(string Text, int? StartMs, int? EndMs, WordOriginDto? Origin, bool LowConfidence)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"StartMs = {StartMs}, EndMs = {EndMs}, Origin = {Origin}, LowConfidence = {LowConfidence}");
        return true;
    }
}
