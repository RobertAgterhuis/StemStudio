using StemStudio.Contracts.Tracks;

namespace StemStudio.Contracts.Lyrics;

/// <summary>The alignment of the track's text (status alignPending or aligning).</summary>
/// <param name="EstimateSeconds">Expected duration on the worker's device.</param>
/// <param name="Device">Where the alignment runs or ran.</param>
/// <param name="Progress">Progress 0..100 while aligning.</param>
public sealed record LyricsAlignmentDto(int? EstimateSeconds, JobDeviceDto? Device, int? Progress);
