using System.Globalization;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Domain.Mixes;

/// <summary>Rules for <see cref="MixSettings.Expanded"/> and <see cref="MixSettings.DetailChannels"/> (data-model.md 3.2).</summary>
internal static class MixDetailRules
{
    /// <summary>Writes the violations of the detail settings into a collector.</summary>
    /// <param name="errors">Collector at the settings root.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="track">The mix's track.</param>
    public static void Validate(ValidationErrors errors, MixSettings settings, Track track)
    {
        var split = SplitStems(track);
        ValidateExpanded(errors, settings.Expanded, split);
        ValidateChannels(errors, settings.DetailChannels, split);
    }

    /// <summary>Detail stem names per split parent; empty unless the detail phase completed.</summary>
    private static Dictionary<string, HashSet<string>> SplitStems(Track track) =>
        (track.DetailPhase is { Status: DetailPhaseStatus.Completed } phase ? phase.Parents : [])
            .Where(parent => parent.Outcome == ParentOutcomeKind.Split)
            .ToDictionary(parent => parent.Parent, parent => parent.Stems.Select(stem => stem.Name).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

    private static void ValidateExpanded(ValidationErrors errors, IReadOnlyList<string> expanded, Dictionary<string, HashSet<string>> split)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < expanded.Count; i++)
        {
            var field = string.Create(CultureInfo.InvariantCulture, $"expanded[{i}]");
            errors.Check(split.ContainsKey(expanded[i]), field, $"The track has no split stem '{expanded[i]}'.");
            errors.Check(seen.Add(expanded[i]), field, "Each parent can be expanded only once.");
        }
    }

    private static void ValidateChannels(ValidationErrors errors, IReadOnlyList<DetailChannelSettings> channels, Dictionary<string, HashSet<string>> split)
    {
        var seen = new HashSet<(string, string)>();
        for (var i = 0; i < channels.Count; i++)
        {
            var channel = channels[i];
            var channelErrors = errors.Nested(string.Create(CultureInfo.InvariantCulture, $"detailChannels[{i}]"));
            var stems = split.GetValueOrDefault(channel.Parent);
            channelErrors.Check(stems is not null, "parent", $"The track has no split stem '{channel.Parent}'.");
            channelErrors.Check(stems is null || stems.Contains(channel.Stem), "stem", $"The track has no detail stem '{channel.Parent}.{channel.Stem}'.");
            channelErrors.Check(seen.Add((channel.Parent, channel.Stem)), "stem", "Only one entry per detail stem is allowed.");
            channel.ValidateRanges(channelErrors);
        }
    }
}
