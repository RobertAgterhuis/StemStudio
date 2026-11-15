using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Mixer;

/// <summary>Detail stem groups of <see cref="MixerState"/>: expansion and the hidden-channel rule (FR-012, FR-013).</summary>
public sealed partial class MixerState
{
    /// <summary>One group per split parent that exists on the track, in standard display order; all start collapsed.</summary>
    public IReadOnlyList<StemGroupState> Groups { get; }

    /// <summary>Gets the group of a parent.</summary>
    /// <param name="parent">The parent stem.</param>
    /// <returns>The group, or null when the parent was not split.</returns>
    public StemGroupState? GroupOf(string parent) => Groups.FirstOrDefault(g => g.Parent == parent);

    /// <summary>Gets the group of a split parent.</summary>
    /// <param name="parent">The parent stem.</param>
    /// <returns>The group.</returns>
    public StemGroupState Group(string parent) =>
        GroupOf(parent) ?? throw new ArgumentException($"Stem '{parent}' has no detail stems.", nameof(parent));

    /// <summary>True when the parent has a group and it is expanded.</summary>
    /// <param name="parent">The parent stem.</param>
    /// <returns>Whether the parent is expanded.</returns>
    public bool IsExpanded(string parent) => GroupOf(parent)?.Expanded == true;

    /// <summary>Expands or collapses a parent and marks the mixer dirty; channel settings are kept (US2 AS3).</summary>
    /// <param name="parent">A split parent.</param>
    /// <param name="expanded">The new state.</param>
    public void SetExpanded(string parent, bool expanded)
    {
        Group(parent).Expanded = expanded;
        IsDirty = true;
    }

    /// <summary>Records that the engine could not expand the parent: it stays collapsed and audible; the dirty flag is unchanged.</summary>
    /// <param name="parent">A split parent.</param>
    public void RejectExpanded(string parent) => Group(parent).Expanded = false;

    /// <summary>
    /// True for channels outside the audible level: detail channels of a collapsed parent and the own channel of
    /// an expanded parent (engine contract 2.0.0).
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>Whether the channel is hidden.</returns>
    public bool IsHidden(ChannelState channel) =>
        Groups.FirstOrDefault(g => g.Contains(channel)) is { } owner ? !owner.Expanded : IsExpanded(channel.Stem);

    private static List<StemGroupState> BuildGroups(TrackDto track, HashSet<string> present)
    {
        var split = track.Detail.Parents
            .Where(p => p.Outcome == ParentOutcomeKindDto.Split && present.Contains(p.Parent))
            .DistinctBy(p => p.Parent)
            .ToDictionary(p => p.Parent, StringComparer.Ordinal);
        return DetailNames.Parents.Where(split.ContainsKey)
            .Select(parent => StemGroupState.From(split[parent]))
            .Where(g => g.Stems.Count > 0)
            .ToList();
    }
}
