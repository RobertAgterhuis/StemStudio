using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Domain.Rendering;

/// <summary>The channels a track offers for an export: its stems and the kept detail stems of every split parent.</summary>
/// <param name="TrackId">The track.</param>
/// <param name="Stems">Standard stems.</param>
/// <param name="Split">Detail stem names per split parent.</param>
public sealed record RenderSource(Guid TrackId, IReadOnlyList<string> Stems, IReadOnlyDictionary<string, IReadOnlyList<string>> Split)
{
    /// <summary>The source of a track (split parents only when its detail phase completed).</summary>
    /// <param name="track">The track.</param>
    /// <returns>The source.</returns>
    public static RenderSource Of(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var parents = track.DetailPhase is { Status: DetailPhaseStatus.Completed } phase ? phase.Parents : [];
        var split = parents.Where(parent => parent.Outcome == ParentOutcomeKind.Split)
            .ToDictionary(parent => parent.Parent, parent => (IReadOnlyList<string>)parent.Stems.Select(stem => stem.Name).ToList(), StringComparer.Ordinal);
        return new RenderSource(track.Id, track.Stems.Select(stem => stem.Name).ToList(), split);
    }

    /// <summary>Every channel in mixer order: standard stems in catalogue order, then the detail stems of each split parent.</summary>
    /// <returns>The channels.</returns>
    public IReadOnlyList<RenderChannel> AllChannels() =>
        Stems.Select(stem => new RenderChannel(stem, null))
            .Concat(Split.SelectMany(parent => parent.Value.Select(name => new RenderChannel(parent.Key, name))))
            .OrderBy(channel => channel.SortKey)
            .ToList();

    /// <summary>True when the track has the channel.</summary>
    /// <param name="channel">The channel.</param>
    /// <returns>Whether it exists.</returns>
    public bool Has(RenderChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        return channel.Detail is null
            ? Stems.Contains(channel.Parent, StringComparer.Ordinal)
            : Split.TryGetValue(channel.Parent, out var names) && names.Contains(channel.Detail, StringComparer.Ordinal);
    }
}

/// <summary>A mixer channel: a standard stem, or a detail stem of a parent (channel id <c>&lt;parent&gt;.&lt;detail&gt;</c>).</summary>
/// <param name="Parent">Standard stem.</param>
/// <param name="Detail">Detail stem, or null for the standard stem itself.</param>
public sealed record RenderChannel(string Parent, string? Detail)
{
    /// <summary>The channel id.</summary>
    public string Id => Detail is null ? Parent : $"{Parent}.{Detail}";

    /// <summary>Mixer order: standard stems first in catalogue order, then detail stems by parent and detail order.</summary>
    public (int Group, int Parent, int Detail) SortKey =>
        Detail is null ? (0, StemCatalog.Order(Parent), 0) : (1, StemCatalog.Order(Parent), DetailCatalog.Order(Parent, Detail));

    /// <summary>The file of the channel relative to the API data directory (data-model 4.3 rule 7).</summary>
    /// <param name="trackId">The track.</param>
    /// <returns>The path with forward slashes.</returns>
    public string PathFor(Guid trackId) => Detail is null
        ? $"tracks/{trackId:D}/stems/{Parent}.flac"
        : $"tracks/{trackId:D}/detail/{Parent}/{Detail}.flac";

    /// <summary>Parses a channel id; null when it is not a standard stem or an allowed detail stem of a parent.</summary>
    /// <param name="id">The channel id.</param>
    /// <returns>The channel or null.</returns>
    public static RenderChannel? Parse(string? id)
    {
        var parts = (id ?? string.Empty).Split('.');
        return parts switch
        {
            [var stem] when StemCatalog.IsKnown(stem) => new RenderChannel(stem, null),
            [var parent, var detail] when DetailCatalog.IsAllowed(parent, detail) => new RenderChannel(parent, detail),
            _ => null,
        };
    }
}
