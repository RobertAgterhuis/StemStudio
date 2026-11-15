using System.Globalization;
using System.Text;

namespace StemStudio.Contracts.Lyrics;

/// <summary>Body of <c>PUT /api/tracks/{id}/lyrics</c>: lyrics entered by the user.</summary>
/// <param name="Text">1..<see cref="ContractLimits.MaxLyricsChars"/> characters; no control characters except line breaks and tabs.</param>
/// <param name="Language">ISO 639 hint for the alignment; null = detect.</param>
/// <remarks><c>ToString()</c> never prints the text, only its length (FR-010).</remarks>
public sealed record SetLyricsRequest(string Text, string? Language = null)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Text = {Text.Length} characters, Language = {Language}");
        return true;
    }
}
