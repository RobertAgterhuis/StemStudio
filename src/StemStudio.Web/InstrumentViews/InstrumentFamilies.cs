using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.InstrumentViews;

/// <summary>
/// The instrument families of the "Practise as" picker and their channels (data-model 5.3): a standard stem belongs
/// to its family, a detail channel to the family of its detail name, and a <c>rest</c> detail to its parent's family.
/// <c>other</c> counts for the orchestral family only while it is not split.
/// </summary>
public static class InstrumentFamilies
{
    private static readonly Dictionary<string, InstrumentFamilyDto> ByStem = new(StringComparer.Ordinal)
    {
        [StemNames.Vocals] = InstrumentFamilyDto.Vocals,
        [StemNames.Drums] = InstrumentFamilyDto.Drums,
        [StemNames.Bass] = InstrumentFamilyDto.Bass,
        [StemNames.Guitar] = InstrumentFamilyDto.Guitar,
        [StemNames.Piano] = InstrumentFamilyDto.Keys,
        [StemNames.Other] = InstrumentFamilyDto.Orchestral,
    };

    private static readonly (string Detail, InstrumentFamilyDto Family)[] DetailTable =
    [
        ("lead", InstrumentFamilyDto.Vocals), ("backing", InstrumentFamilyDto.Vocals), ("kick", InstrumentFamilyDto.Drums),
        ("snare", InstrumentFamilyDto.Drums), ("toms", InstrumentFamilyDto.Drums), ("hihat", InstrumentFamilyDto.Drums),
        ("cymbals", InstrumentFamilyDto.Drums), ("percussion", InstrumentFamilyDto.Drums), ("acoustic-guitar", InstrumentFamilyDto.Guitar),
        ("electric-guitar", InstrumentFamilyDto.Guitar), ("keys", InstrumentFamilyDto.Keys), ("synth", InstrumentFamilyDto.Keys),
        ("strings", InstrumentFamilyDto.Orchestral), ("brass", InstrumentFamilyDto.Orchestral), ("woodwinds", InstrumentFamilyDto.Orchestral),
    ];

    private static readonly Dictionary<string, InstrumentFamilyDto> ByDetail = DetailTable.ToDictionary(p => p.Detail, p => p.Family, StringComparer.Ordinal);

    /// <summary>Every family in picker order (every family counts equally; the order follows spec US6).</summary>
    public static IReadOnlyList<InstrumentFamilyDto> All { get; } =
    [
        InstrumentFamilyDto.Vocals, InstrumentFamilyDto.Guitar, InstrumentFamilyDto.Bass,
        InstrumentFamilyDto.Keys, InstrumentFamilyDto.Drums, InstrumentFamilyDto.Orchestral,
    ];

    /// <summary>The contract id of a family, as used in <c>practise-as-&lt;family&gt;</c> and the preferences.</summary>
    /// <param name="family">The family.</param>
    /// <returns>E.g. <c>orchestral</c>.</returns>
    public static string Id(InstrumentFamilyDto family) => family.ToString().ToLowerInvariant();

    /// <summary>Parses a contract id.</summary>
    /// <param name="id">The id, or null.</param>
    /// <returns>The family, or null when the id is unknown.</returns>
    public static InstrumentFamilyDto? Parse(string? id) => All.Where(f => Id(f) == id).Select(f => (InstrumentFamilyDto?)f).FirstOrDefault();

    /// <summary>The visible name of a family.</summary>
    /// <param name="family">The family.</param>
    /// <returns>E.g. "Strings / brass / woodwinds".</returns>
    public static string Label(InstrumentFamilyDto family) => family switch
    {
        InstrumentFamilyDto.Orchestral => "Strings / brass / woodwinds",
        _ => family.ToString(),
    };

    /// <summary>The view a family opens (spec US6 AS1).</summary>
    /// <param name="family">The family.</param>
    /// <returns>The view.</returns>
    public static InstrumentViewDto View(InstrumentFamilyDto family) => family switch
    {
        InstrumentFamilyDto.Vocals => InstrumentViewDto.Singing,
        InstrumentFamilyDto.Drums => InstrumentViewDto.DrumTrainer,
        InstrumentFamilyDto.Keys => InstrumentViewDto.Chords,
        InstrumentFamilyDto.Orchestral => InstrumentViewDto.Part,
        _ => InstrumentViewDto.Tablature,
    };

    /// <summary>The family of an engine channel id, without the split rule of <c>other</c>.</summary>
    /// <param name="channel">A stem name or <c>&lt;parent&gt;.&lt;detail&gt;</c>.</param>
    /// <returns>The family, or null for an unknown channel.</returns>
    public static InstrumentFamilyDto? FamilyOfChannel(string channel)
    {
        var dot = channel.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0)
        {
            return ByStem.TryGetValue(channel, out var family) ? family : null;
        }

        var detail = channel[(dot + 1)..];
        return detail == DetailNames.Rest ? FamilyOfChannel(channel[..dot]) : ByDetail.TryGetValue(detail, out var byName) ? byName : null;
    }

    /// <summary>
    /// True when the channel belongs to the family on this track: by its name (the split <c>other</c> excepted), or as the
    /// parent of a group whose details all belong to the family (a collapsed group plays its parent; a <c>rest</c> detail
    /// counts for its parent's family; decision 2 of 2026-11-23, data-model 5.3).
    /// </summary>
    /// <param name="state">The mixer.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="family">The family.</param>
    /// <returns>Whether the channel is one of the family's channels.</returns>
    public static bool Contains(MixerState state, ChannelState channel, InstrumentFamilyDto family) =>
        (FamilyOfChannel(channel.Stem) == family && !(channel.Stem == StemNames.Other && state.GroupOf(StemNames.Other) is not null))
        || ParentOfFamilyGroup(state, channel.Stem, family);

    private static bool ParentOfFamilyGroup(MixerState state, string stem, InstrumentFamilyDto family) =>
        state.GroupOf(stem) is { Stems.Count: > 0 } group && group.Stems.All(s => FamilyOfChannel(s.Channel.Stem) == family);
}
