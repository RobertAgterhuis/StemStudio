using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Services;

namespace StemStudio.Web.Mixer;

/// <summary>Detail stem groups of <see cref="MixerState"/>: expansion and the hidden-channel rule (FR-012, FR-013).</summary>
public sealed partial class MixerState
{
    private readonly DateTimeOffset? _detailFilesRemovedAt;
    private IReadOnlyList<string> _keptExpanded = [];
    private IReadOnlyList<DetailChannelSettingsDto> _keptDetailChannels = [];

    /// <summary>True when the clean-up removed the track's detail stems (013 FR-025): no groups, parents play.</summary>
    public bool DetailRemoved => _detailFilesRemovedAt is not null;

    /// <summary>True when the detail stems are removed but the loaded mix has detail settings (<c>mixer-detail-removed</c>).</summary>
    public bool KeepsRemovedDetailSettings => DetailRemoved && (_keptExpanded.Count > 0 || _keptDetailChannels.Count > 0);

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

    /// <summary>Every channel: the standard stems first, then the detail channels of each group in display order.</summary>
    public IEnumerable<ChannelState> AllChannels => Channels.Concat(Groups.SelectMany(g => g.Stems.Select(s => s.Channel)));

    // A mix of a track whose detail stems were removed keeps its detail settings: they are saved back unchanged.
    private void KeepRemovedDetail(MixSettingsDto settings)
    {
        if (DetailRemoved)
        {
            (_keptExpanded, _keptDetailChannels) = (settings.Expanded, settings.DetailChannels);
        }
    }

    private static List<StemGroupState> BuildGroups(TrackDto track) =>
        DetailPhaseText.SplitParents(track).Select(StemGroupState.From).ToList();
}
