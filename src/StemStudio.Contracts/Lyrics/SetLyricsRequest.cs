namespace StemStudio.Contracts.Lyrics;

/// <summary>Body of <c>PUT /api/tracks/{id}/lyrics</c>: lyrics entered by the user.</summary>
/// <param name="Text">1..<see cref="ContractLimits.MaxLyricsChars"/> characters; no control characters except line breaks and tabs.</param>
/// <param name="Language">ISO 639 hint for the alignment; null = detect.</param>
public sealed record SetLyricsRequest(string Text, string? Language = null);
