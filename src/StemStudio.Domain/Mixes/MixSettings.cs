using System.Globalization;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Mixes;

/// <summary>The editable part of a mix: name and mixer state.</summary>
public sealed record MixSettings
{
    /// <summary>Maximum name length after trimming.</summary>
    public const int MaxNameLength = 100;

    /// <summary>Name, 1..100 characters after trimming; unique per track (case-insensitive).</summary>
    public required string Name { get; init; }

    /// <summary>Master volume 0..1.</summary>
    public double MasterVolume { get; init; } = 1.0;

    /// <summary>At most one entry per stem of the track.</summary>
    public IReadOnlyList<ChannelSettings> Channels { get; init; } = [];

    /// <summary>Metronome settings.</summary>
    public required MetronomeSettings Metronome { get; init; }

    /// <summary>Loop region, or null.</summary>
    public LoopRegion? Loop { get; init; }

    /// <summary>Checks every rule against the track the mix belongs to.</summary>
    /// <param name="track">The mix's track (must be completed).</param>
    /// <returns>Violations with JSON-style paths (e.g. <c>metronome.bpmOverride</c>); empty when valid.</returns>
    public IReadOnlyList<DomainError> Validate(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var errors = new ValidationErrors();
        errors.Check(track.Status == TrackStatus.Completed, "trackId", "Mixes can only be saved for tracks that finished processing.");
        var length = Name.Trim().Length;
        errors.Check(length is > 0 and <= MaxNameLength, "name", "Enter a name of 1 to 100 characters.");
        errors.Range("masterVolume", MasterVolume, Bounds.Unit);
        ValidateChannels(errors, track.Stems.Select(stem => stem.Name).ToHashSet(StringComparer.Ordinal));
        Metronome.Validate(errors.Nested("metronome"));
        Loop?.Validate(errors.Nested("loop"), track.DurationSeconds ?? 0);
        return errors.Errors;
    }

    private void ValidateChannels(ValidationErrors errors, HashSet<string> trackStems)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < Channels.Count; i++)
        {
            var channel = Channels[i];
            var channelErrors = errors.Nested(string.Create(CultureInfo.InvariantCulture, $"channels[{i}]"));
            channelErrors.Check(trackStems.Contains(channel.Stem), "stem", $"The track has no stem '{channel.Stem}'.");
            channelErrors.Check(seen.Add(channel.Stem), "stem", "Only one entry per stem is allowed.");
            channel.ValidateRanges(channelErrors);
        }
    }
}
