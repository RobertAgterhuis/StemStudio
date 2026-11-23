#pragma warning disable IDE0005, CS9113, CS1998, IDE0060, CA1822, CA1801, IDE0051, IDE0052, CS0169, CS0414
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Domain.Insights;

/// <summary>Tip codes of data-model 5.3 (no GPU tip, FR-006; the G2 re-separation tip is deferred).</summary>
public enum TipCode
{
    /// <summary>Mute: probably not in the song.</summary>
    NotInSong,

    /// <summary>Use a better detail stem.</summary>
    UseDetailStem,

    /// <summary>Split further.</summary>
    TryDetailSeparation,

    /// <summary>Listen to the rest stem too.</summary>
    ListenToRest,

    /// <summary>Hard to isolate.</summary>
    HardToIsolate,

    /// <summary>Clean enough.</summary>
    Clean,
}

/// <summary>What a tip's button does.</summary>
public enum TipActionKind
{
    /// <summary>Mute the stem in the current mix.</summary>
    Mute,

    /// <summary>Open the mixer with the detail stem expanded.</summary>
    OpenDetail,

    /// <summary>Start the 002 detail separation.</summary>
    SplitDetail,
}

/// <summary>A tip's action.</summary>
/// <param name="Kind">Kind.</param>
/// <param name="Channel">ChannelId of the stem the tip is about.</param>
/// <param name="Parent">Parent stem for detail actions.</param>
/// <param name="Child">Detail stem for <see cref="TipActionKind.OpenDetail"/>.</param>
public sealed record TipAction(TipActionKind Kind, string Channel, string? Parent, string? Child);

/// <summary>A plain-language tip.</summary>
/// <param name="Code">Code.</param>
/// <param name="Text">Text.</param>
/// <param name="Action">Action, or null for advice only.</param>
public sealed record Tip(TipCode Code, string Text, TipAction? Action);

/// <summary>The track state the tips depend on (it changes without new audio, so tips are derived on read).</summary>
/// <param name="Stems">The measures of the report.</param>
/// <param name="Detail">The track's detail phase, or null.</param>
public sealed record TipContext(IReadOnlyList<StemQuality> Stems, DetailPhase? Detail);

/// <summary>
/// The tip rules of data-model 5.3, evaluated in order; the first two that apply are returned (FR-005). There is deliberately
/// no "run on the GPU" tip (FR-006), and rule 3 (high-quality re-separation) is never produced while gate G2 is pending.
/// </summary>
public static class TipRules
{
    /// <summary>Most tips per stem.</summary>
    public const int MaxTips = 2;

    private static readonly HashSet<string> Splittable = ["vocals", "drums", "guitar", "piano", "other"];
    private static readonly HashSet<string> HardFamilies = ["guitar", "piano", "other"];

    /// <summary>The tips of one stem.</summary>
    /// <param name="stem">The stem's measures.</param>
    /// <param name="context">The track state.</param>
    /// <returns>At most two tips, in rule order.</returns>
    public static IReadOnlyList<Tip> For(StemQuality stem, TipContext context)
    {
        throw new NotImplementedException();
    }

    private static bool Add(List<Tip> tips, Tip? tip)
    {
        if (tip is not null)
        {
            tips.Add(tip);
        }

        return tip is not null;
    }

    private static Tip? NotInSong(StemQuality stem) => stem.Rating == QualityRating.NotPresent
        ? new Tip(TipCode.NotInSong, "Mute this stem; the instrument is probably not in this song.", new TipAction(TipActionKind.Mute, stem.Channel, null, null))
        : null;

    private static Tip? UseDetailStem(StemQuality stem, TipContext context)
    {
        if (IsDetail(stem.Channel) || Rank(stem.Rating) is not { } parentRank || SplitParent(context.Detail, stem.Channel) is not { } outcome)
        {
            return null;
        }

        var better = outcome.Stems.Where(child => child.Name != DetailCatalog.Rest)
            .Select(child => (child.Name, Rank: Rank(RatingOf(context, $"{stem.Channel}.{child.Name}"))))
            .Where(child => child.Rank < parentRank)
            .OrderBy(child => child.Rank)
            .Select(child => child.Name)
            .FirstOrDefault();
        return better is null
            ? null
            : new Tip(TipCode.UseDetailStem, $"Use the {Display(better)} detail stem instead.", new TipAction(TipActionKind.OpenDetail, stem.Channel, stem.Channel, better));
    }

    private static Tip? TryDetailSeparation(StemQuality stem, TipContext context) =>
        Splittable.Contains(stem.Channel) && IsFairOrPoor(stem.Rating) && HasNoDetail(context.Detail)
            ? new Tip(TipCode.TryDetailSeparation, "Split further (detail separation) may pull apart the instruments mixed into this stem.", new TipAction(TipActionKind.SplitDetail, stem.Channel, stem.Channel, null))
            : null;

    private static Tip? ListenToRest(StemQuality stem) =>
        IsDetail(stem.Channel) && Child(stem.Channel) != DetailCatalog.Rest && stem.Artefacts.Kinds.Contains(ArtefactKind.RestShare)
            ? new Tip(TipCode.ListenToRest, "Part of this sound went to the 'rest' stem; listen to it too.", new TipAction(TipActionKind.OpenDetail, stem.Channel, Parent(stem.Channel), DetailCatalog.Rest))
            : null;

    private static Tip? HardToIsolate(StemQuality stem) =>
        stem.Rating == QualityRating.Poor || (stem.Rating == QualityRating.Fair && HardFamilies.Contains(Parent(stem.Channel)))
            ? new Tip(TipCode.HardToIsolate, $"{Capitalised(Instrument(stem.Channel))} is hard to isolate with today's models; this is close to the best StemStudio can do.", null)
            : null;

    private static Tip? Clean(StemQuality stem) =>
        stem.Rating == QualityRating.Good ? new Tip(TipCode.Clean, "Clean enough to practise with.", null) : null;

    private static ParentOutcome? SplitParent(DetailPhase? detail, string parent) =>
        detail is { Status: DetailPhaseStatus.Completed, FilesRemovedAt: null }
            ? detail.Parents.FirstOrDefault(outcome => outcome.Parent == parent && outcome.Outcome == ParentOutcomeKind.Split)
            : null;

    private static bool HasNoDetail(DetailPhase? detail) =>
        detail is null or { Status: DetailPhaseStatus.Failed or DetailPhaseStatus.Cancelled } or { Status: DetailPhaseStatus.Completed, FilesRemovedAt: not null };

    private static QualityRating? RatingOf(TipContext context, string channel) =>
        context.Stems.FirstOrDefault(other => other.Channel == channel)?.Rating;

    private static int? Rank(QualityRating? rating) => rating switch
    {
        QualityRating.Good => 0,
        QualityRating.Fair => 1,
        QualityRating.Poor => 2,
        _ => null,
    };

    private static bool IsFairOrPoor(QualityRating rating) => rating is QualityRating.Fair or QualityRating.Poor;

    private static bool IsDetail(string channel) => channel.Contains('.', StringComparison.Ordinal);

    private static string Parent(string channel) => IsDetail(channel) ? channel[..channel.IndexOf('.', StringComparison.Ordinal)] : channel;

    private static string Child(string channel) => channel[(channel.IndexOf('.', StringComparison.Ordinal) + 1)..];

    private static string Instrument(string channel) => Display(IsDetail(channel) ? Child(channel) : channel);

    private static string Display(string name) => name.Replace('-', ' ');

    private static string Capitalised(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
