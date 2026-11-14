using StemStudio.Api.Tracks;
using StemStudio.Contracts.Mixes;
using StemStudio.Domain.Mixes;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Mixes;

/// <summary>Maps mixes between the Domain and the contract DTOs.</summary>
public static class MixMapper
{
    /// <summary>Maps a mix.</summary>
    /// <param name="mix">The mix.</param>
    /// <returns>The DTO.</returns>
    public static MixDto ToDto(Mix mix)
    {
        ArgumentNullException.ThrowIfNull(mix);
        return new MixDto(mix.Id, mix.TrackId, ToDto(mix.Settings), TrackMapper.Utc(mix.CreatedAt), TrackMapper.Utc(mix.UpdatedAt));
    }

    /// <summary>Maps a mix-library entry.</summary>
    /// <param name="summary">The entry.</param>
    /// <returns>The DTO.</returns>
    public static MixSummaryDto ToDto(MixSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new MixSummaryDto(summary.Id, summary.TrackId, summary.TrackTitle, summary.Name, TrackMapper.Utc(summary.UpdatedAt));
    }

    /// <summary>Maps settings to the DTO.</summary>
    /// <param name="settings">Domain settings.</param>
    /// <returns>The DTO.</returns>
    public static MixSettingsDto ToDto(MixSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var metronome = settings.Metronome;
        return new MixSettingsDto(
            settings.Name,
            settings.MasterVolume,
            settings.Channels.Select(c => new ChannelSettingsDto(c.Stem, c.Volume, c.Pan, c.Mute, c.Solo)).ToList(),
            new MetronomeSettingsDto(metronome.Enabled, metronome.Volume, metronome.BpmOverride, metronome.OffsetMs, metronome.BeatsPerBar),
            settings.Loop is { } loop ? new LoopRegionDto(loop.Start, loop.End, loop.Enabled) : null);
    }

    /// <summary>Maps a DTO whose required parts are present (checked by <see cref="MixValidator"/>).</summary>
    /// <param name="dto">The DTO.</param>
    /// <returns>Domain settings (not yet validated against a track).</returns>
    public static MixSettings ToDomain(MixSettingsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var metronome = dto.Metronome;
        return new MixSettings
        {
            Name = dto.Name,
            MasterVolume = dto.MasterVolume,
            Channels = dto.Channels.Select(c => new ChannelSettings { Stem = c.Stem, Volume = c.Volume, Pan = c.Pan, Mute = c.Mute, Solo = c.Solo }).ToList(),
            Metronome = new MetronomeSettings
            {
                Enabled = metronome.Enabled,
                Volume = metronome.Volume,
                BpmOverride = metronome.BpmOverride,
                OffsetMs = metronome.OffsetMs,
                BeatsPerBar = metronome.BeatsPerBar,
            },
            Loop = dto.Loop is { } loop ? new LoopRegion(loop.Start, loop.End, loop.Enabled) : null,
        };
    }
}
