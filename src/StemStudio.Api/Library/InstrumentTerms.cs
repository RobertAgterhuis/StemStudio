using System.Collections.Concurrent;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.Library;

/// <summary>An instrument label of an item and the folded words it is found by.</summary>
/// <param name="Label">Display label ("Snare", "Lead vocals", "Mix").</param>
/// <param name="Words">Folded search words (id words, label, family aliases).</param>
public sealed record InstrumentTerm(string Label, IReadOnlyList<string> Words);

/// <summary>
/// The instrument words of an item (feature 022, research R-08, FR-024/FR-036): every channel it contains with the same
/// structure for every instrument family (<see cref="InstrumentLabels"/>), the 008 profile of sheet music, and "Mix" for
/// mixes and generated takes. Also the item's place in the mixer order of a song folder (FR-018). Pure.
/// </summary>
public sealed class InstrumentTerms
{
    private const int StemGroup = 0;
    private const int DetailGroup = 1;
    private const int MixGroup = 2;
    private const int ScoreGroup = 3;
    private const int GenerationGroup = 4;

    private static readonly ConcurrentDictionary<string, InstrumentTerm> Cache = new(StringComparer.Ordinal);

    private static readonly InstrumentTerm MixTerm = Term(InstrumentLabels.Mix, [InstrumentLabels.Mix]);

    private static readonly string[] ChannelOrder = [.. InstrumentLabels.Channels.Select(channel => channel.ChannelId)];

    private InstrumentTerms(IReadOnlyList<InstrumentTerm> terms, int group, int order) => (Terms, Group, Order) = (terms, group, order);

    /// <summary>The labels with their words, in catalogue order.</summary>
    public IReadOnlyList<InstrumentTerm> Terms { get; }

    /// <summary>Mixer order group: stems, detail stems, mixes, sheet music, generated takes.</summary>
    public int Group { get; }

    /// <summary>Order inside the group (catalogue position of the channel).</summary>
    public int Order { get; }

    /// <summary>The terms of an item.</summary>
    /// <param name="asset">The item.</param>
    /// <returns>The terms.</returns>
    public static InstrumentTerms Of(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var provenance = asset.Provenance;
        return provenance switch
        {
            { ScoreRecipe: { } score } => Single(ProfileChannel(score.Profile), score.Profile.ToString(), ScoreGroup),
            { Recipe: { } recipe } => OfExport(recipe),
            _ => new InstrumentTerms([MixTerm], GenerationGroup, 0),
        };
    }

    private static InstrumentTerms OfExport(ExportRecipe recipe)
    {
        var channels = ChannelsOf(recipe).Where(id => InstrumentLabels.TryGetLabel(id, out _)).Distinct(StringComparer.Ordinal).ToList();
        if (recipe.Mode == ExportMode.Raw && channels is [var single])
        {
            return new InstrumentTerms([ChannelTerm(single)], single.Contains('.', StringComparison.Ordinal) ? DetailGroup : StemGroup, IndexOf(single));
        }

        var terms = channels.OrderBy(IndexOf).Select(ChannelTerm).Append(MixTerm);
        return new InstrumentTerms([.. terms], MixGroup, 0);
    }

    private static IEnumerable<string> ChannelsOf(ExportRecipe recipe)
    {
        if (recipe.Channels is { } channels)
        {
            return channels;
        }

        var mix = recipe.Mix;
        return mix is null ? [] : mix.Channels.Select(channel => channel.Stem).Concat(mix.DetailChannels.Select(detail => $"{detail.Parent}.{detail.Stem}"));
    }

    private static InstrumentTerms Single(string channel, string label, int group) =>
        new([Cache.GetOrAdd($"{label}{channel}", _ => Term(label, [label, .. InstrumentLabels.SearchWordsOf(channel)]))], group, IndexOf(channel));

    private static InstrumentTerm ChannelTerm(string channelId) => Cache.GetOrAdd(channelId, id =>
    {
        InstrumentLabels.TryGetLabel(id, out var label);
        return Term(label!, InstrumentLabels.SearchWordsOf(id));
    });

    private static InstrumentTerm Term(string label, IEnumerable<string> words) =>
        new(label, [.. words.Select(word => Domain.Library.LibraryKey.Fold(word).Value).Distinct(StringComparer.Ordinal)]);

    private static int IndexOf(string channelId) => Array.IndexOf(ChannelOrder, channelId) is var index and >= 0 ? index : ChannelOrder.Length;

    private static string ProfileChannel(ScoreProfile profile) => profile switch
    {
        ScoreProfile.Vocals => StemNames.Vocals,
        ScoreProfile.Bass => StemNames.Bass,
        ScoreProfile.Guitar => StemNames.Guitar,
        ScoreProfile.Keys => $"{StemNames.Piano}.keys",
        ScoreProfile.Piano => StemNames.Piano,
        _ => StemNames.Drums,
    };
}
