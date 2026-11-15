namespace StemStudio.Contracts.Mixes;

/// <summary>The editable part of a mix; also the body of <c>PUT /api/mixes/{id}</c>.</summary>
/// <param name="Name">Name, 1..100 characters after trimming, unique per track (case-insensitive).</param>
/// <param name="MasterVolume">Master volume 0..1.</param>
/// <param name="Channels">At most one entry per stem of the track; stems without an entry use defaults.</param>
/// <param name="Metronome">Metronome settings.</param>
/// <param name="Loop">Loop region, or null when none is set.</param>
public sealed record MixSettingsDto(
    string Name,
    double MasterVolume,
    IReadOnlyList<ChannelSettingsDto> Channels,
    MetronomeSettingsDto Metronome,
    LoopRegionDto? Loop)
{
    /// <summary>Parents shown expanded (each must be split on the track); empty = all collapsed (mixes saved before 1.2.0).</summary>
    public IReadOnlyList<string> Expanded { get; init; } = [];

    /// <summary>At most one entry per (parent, detail stem); detail stems without an entry use defaults.</summary>
    public IReadOnlyList<DetailChannelSettingsDto> DetailChannels { get; init; } = [];
}
