using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Services;

namespace StemStudio.Web.LibraryCare;

/// <summary>A column of the per-track storage table (ui-test-ids 1.12.0 <c>&lt;column&gt;</c>).</summary>
/// <param name="Id">The id part of <c>storage-sort-&lt;column&gt;</c>.</param>
/// <param name="Label">The header text.</param>
/// <param name="Bytes">The value of a track (null when not measured).</param>
public sealed record StorageColumn(string Id, string Label, Func<TrackStorageDto, long?> Bytes);

/// <summary>One line of the storage areas: a volume and the volumes on the same disk.</summary>
/// <param name="Volume">The first volume of the disk.</param>
/// <param name="SharedWith">The test-id parts of the other volumes on that disk.</param>
public sealed record VolumeLine(StorageVolumeDto Volume, IReadOnlyList<string> SharedWith)
{
    /// <summary>Used share 0-100, or null when the sizes are not known.</summary>
    public int? UsedPercent => Volume is { FreeBytes: { } free, TotalBytes: { } total and > 0 }
        ? (int)Math.Round(100.0 * (total - free) / total)
        : null;
}

/// <summary>Rules of the Storage tab (013 US3, FR-020, FR-021): volume lines, manage links, the table columns and sorting.</summary>
public static class StorageView
{
    /// <summary>The columns of the per-track table, in order.</summary>
    public static IReadOnlyList<StorageColumn> Columns { get; } =
    [
        new("total", "Total", t => t.TotalBytes),
        new("source", "Source copy", t => t.SourceBytes),
        new("stems", "Stems", t => t.StemsBytes),
        new("detail-stems", "Detail stems", t => t.DetailStemsBytes),
        new("artwork", "Artwork", t => t.ArtworkBytes),
        new("exports", "Exports", t => t.ExportsBytes),
        new("scores", "Sheet music", t => t.ScoresBytes),
        new("generations", "Generations", t => t.GenerationsBytes),
        new("recordings", "Recordings", t => t.RecordingsBytes),
        new("worker-cache", "Worker cache", t => t.WorkerCacheBytes),
    ];

    /// <summary>The test-id part of a volume (<c>workerData</c> -&gt; <c>worker-data</c>).</summary>
    /// <param name="id">The volume id.</param>
    /// <returns>The id part.</returns>
    public static string VolumeId(StorageVolumeIdDto id) => CareText.Kebab(id);

    /// <summary>One line per disk: a volume that shares its disk with a volume shown before it is folded into that line.</summary>
    /// <param name="volumes">The volumes of the overview.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<VolumeLine> Lines(IReadOnlyList<StorageVolumeDto> volumes)
    {
        var shown = new HashSet<string>(StringComparer.Ordinal);
        var lines = new List<VolumeLine>();
        foreach (var volume in volumes)
        {
            var wire = WireName.Of(volume.Id);
            if (shown.Add(wire) && !volume.SharedWith.Any(shown.Contains))
            {
                lines.Add(new VolumeLine(volume, [.. volume.SharedWith.Select(Kebab)]));
            }
        }

        return lines;
    }

    /// <summary>The readable names of the volumes of a line, e.g. "Library, Worker data and Model cache".</summary>
    /// <param name="line">The line.</param>
    /// <param name="volumes">Every volume (for the labels of the shared ones).</param>
    /// <returns>The names.</returns>
    public static string LineLabel(VolumeLine line, IReadOnlyList<StorageVolumeDto> volumes)
    {
        var names = new List<string> { line.Volume.Label };
        names.AddRange(line.Volume.SharedWith.Select(id => volumes.FirstOrDefault(v => WireName.Of(v.Id) == id)?.Label ?? id));
        return names.Count == 1 ? names[0] : $"{string.Join(", ", names[..^1])} and {names[^1]} (same disk)";
    }

    /// <summary>The free-space text of a volume line.</summary>
    /// <param name="line">The line.</param>
    /// <returns>"100 GB free of 400 GB" or "Not available: &lt;reason&gt;".</returns>
    public static string FreeText(VolumeLine line) => line.Volume is { FreeBytes: { } free, TotalBytes: { } total }
        ? $"{ByteSize.Format(free)} free of {ByteSize.Format(total)}"
        : $"Not available: {line.Volume.Reason ?? "it cannot be measured"}";

    /// <summary>The screen where the items of an original category are deleted one by one, or null.</summary>
    /// <param name="category">The category.</param>
    /// <returns>A relative link, or null.</returns>
    public static string? ManageLink(StorageCategoryDto category) => category switch
    {
        StorageCategoryDto.Database or StorageCategoryDto.Stems or StorageCategoryDto.Artwork or StorageCategoryDto.PendingUploads
            or StorageCategoryDto.Recordings => "tracks",
        StorageCategoryDto.Exports or StorageCategoryDto.Scores => "library",
        StorageCategoryDto.Generations => "generations",
        StorageCategoryDto.VoiceProfiles => "voices",
        StorageCategoryDto.PreviousLibrary => "care",
        _ => null,
    };

    /// <summary>A size, or "Not measured" when it is not known.</summary>
    /// <param name="bytes">The size.</param>
    /// <returns>The text.</returns>
    public static string Size(long? bytes) => bytes is { } value ? ByteSize.Format(value) : "Not measured";

    /// <summary>The tracks sorted by a column (unknown sizes last).</summary>
    /// <param name="tracks">The tracks.</param>
    /// <param name="column">The column id.</param>
    /// <param name="descending">Largest first.</param>
    /// <returns>The sorted tracks.</returns>
    public static IReadOnlyList<TrackStorageDto> Sort(IEnumerable<TrackStorageDto> tracks, string column, bool descending)
    {
        var bytes = Columns.First(c => c.Id == column).Bytes;
        var known = tracks.OrderBy(t => bytes(t) is null);
        return descending ? [.. known.ThenByDescending(t => bytes(t) ?? 0)] : [.. known.ThenBy(t => bytes(t) ?? 0)];
    }

    /// <summary>The five largest tracks (Beginner list).</summary>
    /// <param name="tracks">The tracks.</param>
    /// <returns>At most five tracks, largest first.</returns>
    public static IReadOnlyList<TrackStorageDto> Top(IEnumerable<TrackStorageDto> tracks) => [.. tracks.OrderByDescending(t => t.TotalBytes).Take(5)];

    private static string Kebab(string wire) => string.Concat(wire.Select(c => char.IsUpper(c) ? $"-{char.ToLowerInvariant(c)}" : c.ToString()));
}
