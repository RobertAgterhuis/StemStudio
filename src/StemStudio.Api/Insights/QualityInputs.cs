using StemStudio.Api.Storage;
using StemStudio.Api.Worker;
using StemStudio.Domain.Insights;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.Insights;

/// <summary>A planned quality run: the worker request and the version of the stem files it reads.</summary>
/// <param name="Request">The worker request.</param>
/// <param name="StemsVersion">The stem files version (<see cref="Domain.Insights.StemsVersion"/>).</param>
public sealed record QualityPlan(WorkerQualityJobRequest Request, string StemsVersion);

/// <summary>
/// The stems of a quality run (data-model 2.3): the six standard stems in catalogue order, then the kept detail and rest stems;
/// the 002 metrics of split parents are not stored, so they are sent as null. Also the files version for <c>outdated</c>.
/// </summary>
/// <param name="stems">Standard stem files.</param>
/// <param name="details">Detail stem files.</param>
public sealed class QualityInputs(IStemFileStore stems, IDetailStemFileStore details)
{
    /// <summary>Longest title sent to the worker.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>True when the track has all six standard stems (the worker sums them to the mix).</summary>
    /// <param name="track">The track.</param>
    /// <returns>True for a six-stem track.</returns>
    public static bool HasSixStems(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var names = track.Stems.Select(stem => stem.Name).ToHashSet(StringComparer.Ordinal);
        return StemCatalog.SixStem.All(names.Contains);
    }

    /// <summary>The channels of a run: standard stems first, then kept detail stems in mixer order.</summary>
    /// <param name="track">A six-stem track.</param>
    /// <returns>The channels.</returns>
    public static IReadOnlyList<RenderChannel> Channels(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return SectionChannels.Of(track).Select(RenderChannel.Parse).OfType<RenderChannel>().OrderBy(channel => channel.SortKey).ToList();
    }

    /// <summary>Plans a run of a six-stem track.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The plan.</returns>
    public QualityPlan Plan(Track track)
    {
        var channels = Channels(track);
        var inputs = channels.Select(channel => new WorkerQualityInput(channel.Id, channel.PathFor(track.Id), Kind(channel))).ToList();
        var parents = track.DetailPhase is { Status: DetailPhaseStatus.Completed, FilesRemovedAt: null } phase
            ? phase.Parents.Where(parent => parent.Outcome == ParentOutcomeKind.Split).Select(parent => new WorkerDetailParentInput(parent.Parent, null, null)).ToList()
            : [];
        var titles = new WorkerQualityTitles(Cut(track.Title) ?? "Untitled", Cut(track.Artist));
        return new QualityPlan(new WorkerQualityJobRequest(inputs, parents, titles), VersionOf(track, channels));
    }

    /// <summary>The version of the track's current stem files.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The version.</returns>
    public string CurrentVersion(Track track) => VersionOf(track, Channels(track));

    private string VersionOf(Track track, IReadOnlyList<RenderChannel> channels) =>
        StemsVersion.Of(channels.Select(channel => Stamp(channel, File(track.Id, channel))));

    private FileInfo? File(Guid trackId, RenderChannel channel) =>
        channel.Detail is null ? stems.FindStem(trackId, channel.Parent) : details.Find(new DetailStemKey(trackId, channel.Parent, channel.Detail));

    private static StemFileStamp Stamp(RenderChannel channel, FileInfo? file) => file is { Exists: true }
        ? new StemFileStamp(channel.Id, file.Length, file.LastWriteTimeUtc)
        : new StemFileStamp(channel.Id, -1, DateTime.MinValue);

    private static WorkerQualityInputKind Kind(RenderChannel channel) => channel.Detail switch
    {
        null => WorkerQualityInputKind.Standard,
        DetailCatalog.Rest => WorkerQualityInputKind.Rest,
        _ => WorkerQualityInputKind.Detail,
    };

    private static string? Cut(string? text) => text is null || text.Length <= MaxTitleLength ? text : text[..MaxTitleLength];
}
