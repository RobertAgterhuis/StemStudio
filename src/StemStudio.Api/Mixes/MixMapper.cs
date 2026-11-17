using StemStudio.Api.Tracks;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Recording;
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
        return new MixSettingsDto(
            settings.Name,
            settings.MasterVolume,
            settings.Channels.Select(c => new ChannelSettingsDto(c.Stem, c.Volume, c.Pan, c.Mute, c.Solo)).ToList(),
            ToDto(settings.Metronome),
            settings.Loop is { } loop ? new LoopRegionDto(loop.Start, loop.End, loop.Enabled, loop.SectionId) : null)
        {
            Expanded = settings.Expanded.ToList(),
            DetailChannels = settings.DetailChannels.Select(c => new DetailChannelSettingsDto(c.Parent, c.Stem, c.Volume, c.Pan, c.Mute, c.Solo)).ToList(),
            Practice = ToDto(settings.Practice),
            Take = settings.Take is { } take ? new TakeChannelSettingsDto { TakeId = take.TakeId, Volume = take.Volume, Pan = take.Pan, Mute = take.Mute, Solo = take.Solo } : null,
        };
    }

    private static MetronomeSettingsDto ToDto(MetronomeSettings metronome) =>
        new(metronome.Enabled, metronome.Volume, metronome.BpmOverride, metronome.OffsetMs, metronome.BeatsPerBar);

    /// <summary>Maps a DTO whose required parts are present (checked by <see cref="MixValidator"/>).</summary>
    /// <param name="dto">The DTO.</param>
    /// <returns>Domain settings (not yet validated against a track).</returns>
    public static MixSettings ToDomain(MixSettingsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MixSettings
        {
            Name = dto.Name,
            MasterVolume = dto.MasterVolume,
            Channels = dto.Channels.Select(ToDomain).ToList(),
            Metronome = ToDomain(dto.Metronome),
            Loop = dto.Loop is { } loop ? new LoopRegion(loop.Start, loop.End, loop.Enabled) { SectionId = loop.SectionId } : null,
            Expanded = (dto.Expanded ?? []).ToList(),
            DetailChannels = (dto.DetailChannels ?? []).Select(ToDomain).ToList(),
            Practice = dto.Practice is { } practice ? ToDomain(practice) : PracticeSettings.Default,
            Take = dto.Take is { } take ? new TakeChannelSettings { TakeId = take.TakeId, Volume = take.Volume, Pan = take.Pan, Mute = take.Mute, Solo = take.Solo } : null,
        };
    }

    /// <summary>Maps practice settings to the DTO (012).</summary>
    /// <param name="practice">Domain settings.</param>
    /// <returns>The DTO.</returns>
    public static PracticeSettingsDto ToDto(PracticeSettings practice)
    {
        ArgumentNullException.ThrowIfNull(practice);
        var (trainer, countIn) = (practice.Trainer, practice.CountIn);
        return new PracticeSettingsDto(
            practice.SpeedPercent,
            practice.TransposeSemitones,
            new SpeedTrainerSettingsDto(trainer.Enabled, trainer.StartPercent, trainer.StepPercent, trainer.TargetPercent, trainer.PassesPerStep),
            new CountInSettingsDto(countIn.Bars, countIn.EveryPass));
    }

    /// <summary>Maps practice settings whose groups are present (checked by <see cref="MixValidator"/>).</summary>
    /// <param name="practice">The DTO.</param>
    /// <returns>Domain settings (not yet validated).</returns>
    internal static PracticeSettings ToDomain(PracticeSettingsDto practice) => new()
    {
        SpeedPercent = practice.SpeedPercent,
        TransposeSemitones = practice.TransposeSemitones,
        Trainer = new SpeedTrainerSettings
        {
            Enabled = practice.Trainer.Enabled,
            StartPercent = practice.Trainer.StartPercent,
            StepPercent = practice.Trainer.StepPercent,
            TargetPercent = practice.Trainer.TargetPercent,
            PassesPerStep = practice.Trainer.PassesPerStep,
        },
        CountIn = new CountInSettings { Bars = practice.CountIn.Bars, EveryPass = practice.CountIn.EveryPass },
    };

    /// <summary>Maps detail channel settings.</summary>
    /// <param name="channel">The DTO.</param>
    /// <returns>Domain settings.</returns>
    internal static DetailChannelSettings ToDomain(DetailChannelSettingsDto channel) => new()
    {
        Parent = channel.Parent,
        Stem = channel.Stem,
        Volume = channel.Volume,
        Pan = channel.Pan,
        Mute = channel.Mute,
        Solo = channel.Solo,
    };

    /// <summary>Maps channel settings.</summary>
    /// <param name="channel">The DTO.</param>
    /// <returns>Domain settings.</returns>
    internal static ChannelSettings ToDomain(ChannelSettingsDto channel) =>
        new() { Stem = channel.Stem, Volume = channel.Volume, Pan = channel.Pan, Mute = channel.Mute, Solo = channel.Solo };

    private static MetronomeSettings ToDomain(MetronomeSettingsDto metronome) => new()
    {
        Enabled = metronome.Enabled,
        Volume = metronome.Volume,
        BpmOverride = metronome.BpmOverride,
        OffsetMs = metronome.OffsetMs,
        BeatsPerBar = metronome.BeatsPerBar,
    };
}
