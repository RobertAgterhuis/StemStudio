using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Domain.Sections;

/// <summary>The channel ids of a track a solo may name: its stems and its split detail stems (<c>parent.stem</c>).</summary>
public static class SectionChannels
{
    /// <summary>The channel ids of a track.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The ids.</returns>
    public static IReadOnlySet<string> Of(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var channels = track.Stems.Select(stem => stem.Name).ToHashSet(StringComparer.Ordinal);
        if (track.DetailPhase is { Status: DetailPhaseStatus.Completed, FilesRemovedAt: null } phase)
        {
            channels.UnionWith(phase.Parents.Where(parent => parent.Outcome == ParentOutcomeKind.Split)
                .SelectMany(parent => parent.Stems.Select(stem => $"{parent.Parent}.{stem.Name}")));
        }

        return channels;
    }
}
