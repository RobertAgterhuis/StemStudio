using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Export;

/// <summary>What the export dialog saves (ui-test-ids 1.5.0 <c>data-mode</c>).</summary>
public enum ExportKind
{
    /// <summary>One raw stem or detail stem from the track page.</summary>
    Stem,

    /// <summary>The mixer: what I hear or selected channels.</summary>
    Mix,

    /// <summary>Every stem as its own raw item.</summary>
    StemPack,
}

/// <summary>A channel the user can pick in "Selected channels".</summary>
/// <param name="Id">The channel id (<c>drums</c>, <c>drums.snare</c>).</param>
/// <param name="Label">The visible name.</param>
public sealed record ExportChannel(string Id, string Label)
{
    /// <summary>The id with the dot written as a dash (<c>export-channel-&lt;key&gt;</c>).</summary>
    public string Key => Id.Replace('.', '-');
}

/// <summary>The source of an export dialog: the track and, for the mixer, a snapshot of its current state.</summary>
/// <param name="Kind">Stem, mix or stem pack.</param>
/// <param name="Track">The completed track.</param>
public sealed partial record ExportTarget(ExportKind Kind, TrackDto Track)
{
    /// <summary>The raw channel (stem mode).</summary>
    public string? Channel { get; init; }

    /// <summary>The current, possibly unsaved mixer state (mix mode).</summary>
    public ExportMixDto? Mix { get; init; }

    /// <summary>Every visible mixer channel in mixer order (mix mode).</summary>
    public IReadOnlyList<ExportChannel> Channels { get; init; } = [];

    /// <summary>Ids of the channels that are audible right now (mix mode; preselection of "Selected channels").</summary>
    public IReadOnlyList<string> Audible { get; init; } = [];

    /// <summary>The mixer's loop region, if any (mix mode).</summary>
    public LoopRegionDto? Loop { get; init; }

    /// <summary>
    /// True while the mixer plays away from the original speed or key (speed, transpose or the speed trainer, feature 012):
    /// the dialog says that exports use the original speed and key (FR-015). The export request is not affected.
    /// </summary>
    public bool AtPracticeTempo { get; init; }

    /// <summary>Length of the song in seconds (range validation).</summary>
    public double Duration { get; init; } = Track.DurationSeconds ?? 0;

    /// <summary>The <c>data-mode</c> value.</summary>
    public string ModeName => Kind switch
    {
        ExportKind.Mix => "mix",
        ExportKind.StemPack => "stem-pack",
        _ => "stem",
    };

    /// <summary>True when the track has split parents with kept detail stems (stem pack: <c>export-include-detail</c>).</summary>
    public bool HasDetail => Track.Detail.FilesRemovedAt is null && Track.Detail.Parents.Any(p => p.Outcome == ParentOutcomeKindDto.Split && p.Stems.Count > 0);

    /// <summary>A raw stem or detail stem of the track page.</summary>
    /// <param name="track">The track.</param>
    /// <param name="channel">The channel id.</param>
    /// <returns>The target.</returns>
    public static ExportTarget ForStem(TrackDto track, string channel) => new(ExportKind.Stem, track) { Channel = channel };

    /// <summary>Every stem of the track.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The target.</returns>
    public static ExportTarget ForStemPack(TrackDto track) => new(ExportKind.StemPack, track);
}
