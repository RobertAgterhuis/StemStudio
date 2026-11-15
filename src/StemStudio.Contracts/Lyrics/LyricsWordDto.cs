namespace StemStudio.Contracts.Lyrics;

/// <summary>One word; times (ms from the track start) and origin are null in a plain document.</summary>
/// <param name="Text">Text (1..100 characters).</param>
/// <param name="StartMs">Start in milliseconds.</param>
/// <param name="EndMs">End in milliseconds (&gt; start, at most start + 3000).</param>
/// <param name="Origin">Where the time comes from.</param>
/// <param name="LowConfidence">Aligned word with a low probability (expert view).</param>
public sealed record LyricsWordDto(string Text, int? StartMs, int? EndMs, WordOriginDto? Origin, bool LowConfidence);
