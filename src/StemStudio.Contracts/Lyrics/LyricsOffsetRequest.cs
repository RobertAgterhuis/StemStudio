namespace StemStudio.Contracts.Lyrics;

/// <summary>Body of <c>PUT /api/tracks/{id}/lyrics/offset</c>.</summary>
/// <param name="OffsetMs">-5000..5000 in steps of <see cref="ContractLimits.LyricsOffsetStepMs"/>; positive = lyrics later.</param>
public sealed record LyricsOffsetRequest(int OffsetMs);
