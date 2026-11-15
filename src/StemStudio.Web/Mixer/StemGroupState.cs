using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Mixer;

/// <summary>The detail stems of one split parent and whether the parent is expanded (FR-012).</summary>
/// <param name="parent">The parent stem.</param>
/// <param name="stems">The detail channels in display order, <c>rest</c> last.</param>
public sealed class StemGroupState(string parent, IReadOnlyList<DetailChannelState> stems)
{
    /// <summary>The parent stem, e.g. <c>drums</c>.</summary>
    public string Parent { get; } = parent;

    /// <summary>The detail channels in display order.</summary>
    public IReadOnlyList<DetailChannelState> Stems { get; } = stems;

    /// <summary>True while the detail stems replace the parent's own audio.</summary>
    public bool Expanded { get; internal set; }

    /// <summary>Builds the group of a split parent; only names allowed for the parent are kept, in display order.</summary>
    /// <param name="outcome">A parent outcome with kind split.</param>
    /// <returns>The group (possibly without stems).</returns>
    public static StemGroupState From(ParentOutcomeDto outcome)
    {
        var names = outcome.Stems.Select(s => s.Name).ToHashSet(StringComparer.Ordinal);
        var stems = DetailNames.AllowedFor(outcome.Parent).Where(names.Contains).Select(n => new DetailChannelState(outcome.Parent, n));
        return new StemGroupState(outcome.Parent, stems.ToList());
    }

    /// <summary>True when the channel is one of this group's detail channels.</summary>
    /// <param name="channel">The channel.</param>
    /// <returns>Whether the channel belongs to the group.</returns>
    public bool Contains(ChannelState channel) => Stems.Any(s => ReferenceEquals(s.Channel, channel));

    /// <summary>Applies the expansion flag and the detail channel entries of a saved mix (defaults when absent).</summary>
    /// <param name="settings">The saved settings.</param>
    public void Apply(MixSettingsDto settings)
    {
        Expanded = settings.Expanded.Contains(Parent, StringComparer.Ordinal);
        foreach (var stem in Stems)
        {
            stem.Apply(settings.DetailChannels.FirstOrDefault(d => d.Parent == Parent && d.Stem == stem.Name));
        }
    }
}
