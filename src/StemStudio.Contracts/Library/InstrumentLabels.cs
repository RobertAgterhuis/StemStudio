using System.Diagnostics.CodeAnalysis;

namespace StemStudio.Contracts.Library;

/// <summary>A channel of an instrument family: a standard stem or one of its detail stems (<c>parent.detail</c>).</summary>
/// <param name="ChannelId">Channel id as in backend-api <c>ChannelId</c> (<c>drums</c>, <c>drums.snare</c>).</param>
/// <param name="Label">English display label (at most <see cref="ContractLimits.InstrumentLabelMaxLength"/> characters).</param>
public sealed record InstrumentChannel(string ChannelId, string Label);

/// <summary>One instrument family: a standard stem, its label, its everyday words and its detail channels.</summary>
/// <param name="Stem">Standard stem name (<see cref="StemNames"/>).</param>
/// <param name="Label">English display label of the stem.</param>
/// <param name="Aliases">Everyday words for the family (searched for the stem and every detail of it).</param>
/// <param name="Details">Detail channels in <see cref="DetailNames.AllowedFor"/> order (<c>rest</c> last); empty for bass.</param>
public sealed record InstrumentFamily(string Stem, string Label, IReadOnlyList<string> Aliases, IReadOnlyList<InstrumentChannel> Details);

/// <summary>
/// The closed catalogue of instrument words (feature 022, research R-08), shared by the API search and the Web labels: one
/// family per standard stem with the same structure for every family (MoSCoW "every instrument counts equally"). A channel
/// is searchable by its id words (<c>.</c> and <c>-</c> as spaces), its label and its family's aliases.
/// </summary>
public static class InstrumentLabels
{
    /// <summary>Label added to every mix item ("What I hear", "Selected channels") and generated take.</summary>
    public const string Mix = "Mix";

    // Labels in the order of DetailNames.Drums, DetailNames.Vocals and DetailNames.Instruments.
    private static readonly string[] DrumLabels = ["Kick", "Snare", "Toms", "Hi-hat", "Cymbals"];

    private static readonly string[] VocalLabels = ["Lead vocals", "Backing vocals"];

    private static readonly string[] FamilyLabels = ["Strings", "Brass", "Woodwinds", "Keys", "Synth", "Acoustic guitar", "Electric guitar", "Percussion"];

    private static readonly Dictionary<string, string> DetailLabels = DetailNames.Drums.Zip(DrumLabels)
        .Concat(DetailNames.Vocals.Zip(VocalLabels)).Concat(DetailNames.Instruments.Zip(FamilyLabels))
        .ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal);

    private static readonly Dictionary<string, (string Label, string RestLabel, string[] Aliases)> Stems = StemTable();

    private static readonly Lazy<Dictionary<string, (InstrumentChannel Channel, InstrumentFamily Family)>> Index = new(BuildIndex);

    /// <summary>The six families in <see cref="StemNames.SixStem"/> order.</summary>
    public static IReadOnlyList<InstrumentFamily> Families { get; } = [.. StemNames.SixStem.Select(Family)];

    /// <summary>Every channel of the catalogue: each stem followed by its detail channels.</summary>
    public static IReadOnlyList<InstrumentChannel> Channels { get; } =
        [.. Families.SelectMany(family => (IEnumerable<InstrumentChannel>)[new InstrumentChannel(family.Stem, family.Label), .. family.Details])];

    /// <summary>The label of a channel id.</summary>
    /// <param name="channelId">A stem (<c>drums</c>) or detail channel (<c>drums.snare</c>).</param>
    /// <param name="label">The label, or null when the id is not in the catalogue.</param>
    /// <returns>True when the id is known.</returns>
    public static bool TryGetLabel(string? channelId, [NotNullWhen(true)] out string? label)
    {
        label = channelId is not null && Index.Value.TryGetValue(channelId, out var entry) ? entry.Channel.Label : null;
        return label is not null;
    }

    /// <summary>The family of a channel id (the stem's family for a detail channel).</summary>
    /// <param name="channelId">A stem or detail channel id.</param>
    /// <returns>The family, or null when the id is not in the catalogue.</returns>
    public static InstrumentFamily? FamilyOf(string? channelId) =>
        channelId is not null && Index.Value.TryGetValue(channelId, out var entry) ? entry.Family : null;

    /// <summary>The words a channel id is found by: the id words, the label and the family's aliases (lower case, distinct).</summary>
    /// <param name="channelId">A stem or detail channel id.</param>
    /// <returns>The words; empty when the id is not in the catalogue.</returns>
    public static IReadOnlyList<string> SearchWordsOf(string? channelId)
    {
        if (channelId is null || !Index.Value.TryGetValue(channelId, out var entry))
        {
            return [];
        }

        var idWords = channelId.Replace('.', ' ').Replace('-', ' ');
        return [.. new[] { idWords, entry.Channel.Label }.Concat(entry.Family.Aliases).Select(word => word.ToLowerInvariant()).Distinct(StringComparer.Ordinal)];
    }

    private static Dictionary<string, (string Label, string RestLabel, string[] Aliases)> StemTable() => new(StringComparer.Ordinal)
    {
        [StemNames.Vocals] = ("Vocals", "Other vocals", ["voice", "vocal", "singing"]),
        [StemNames.Drums] = ("Drums", "Other drums", ["drum", "drum kit", "percussion kit"]),
        [StemNames.Bass] = ("Bass", "Other bass sounds", ["bass guitar", "bassline", "low end"]),
        [StemNames.Guitar] = ("Guitar", "Other guitar sounds", ["guitars", "gtr", "guitar part"]),
        [StemNames.Piano] = ("Piano", "Other piano sounds", ["pianos", "keyboard", "piano part"]),
        [StemNames.Other] = ("Other", "Other sounds", ["orchestra", "horns", "other instruments"]),
    };

    private static InstrumentFamily Family(string stem)
    {
        var (label, _, aliases) = Stems[stem];
        var details = DetailNames.AllowedFor(stem).Select(detail => new InstrumentChannel($"{stem}.{detail}", DetailLabel(stem, detail)));
        return new InstrumentFamily(stem, label, aliases, [.. details]);
    }

    private static string DetailLabel(string stem, string detail) => detail == DetailNames.Rest ? Stems[stem].RestLabel : DetailLabels[detail];

    private static Dictionary<string, (InstrumentChannel, InstrumentFamily)> BuildIndex() =>
        Families.SelectMany(family => Channels.Where(channel => channel.ChannelId == family.Stem || channel.ChannelId.StartsWith(family.Stem + ".", StringComparison.Ordinal))
                .Select(channel => (channel, family)))
            .ToDictionary(pair => pair.channel.ChannelId, pair => (pair.channel, pair.family), StringComparer.Ordinal);
}
