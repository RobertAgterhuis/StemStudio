using Bunit;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Components.Care;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Care;

/// <summary>The per-track storage table (013 T056, FR-020, FR-032; ui-test-ids 1.12.0 storage-tracks-table, storage-sort-*).</summary>
public sealed class StorageTracksTableTests : PageTestContext
{
    private static readonly TrackStorageDto Big = StorageData.Track(StorageData.TrackA, "Big song", 900 * StorageData.MiB, 600 * StorageData.MiB);
    private static readonly TrackStorageDto Small = StorageData.Track(StorageData.TrackB, "Small song", 100 * StorageData.MiB) with
    {
        DetailFilesRemoved = true,
        WorkerCacheBytes = 50 * StorageData.MiB,
        SourceBytes = 10 * StorageData.MiB,
    };

    private IRenderedComponent<StorageTracksTable> RenderTable() => Render<StorageTracksTable>(p => p.Add(t => t.Tracks, [Small, Big]));

    private static List<string?> Order(IRenderedComponent<StorageTracksTable> table) =>
        [.. table.FindAll("tbody tr").Select(r => r.GetAttribute("data-testid"))];

    [Fact]
    public void Table_HasASortButtonPerColumn_AndSortsByTotalFirst()
    {
        var table = RenderTable();

        table.FindAll("thead button").Select(b => b.GetAttribute("data-testid")).ShouldBe(
            ["storage-sort-total", "storage-sort-source", "storage-sort-stems", "storage-sort-detail-stems", "storage-sort-artwork", "storage-sort-exports",
                "storage-sort-scores", "storage-sort-generations", "storage-sort-recordings", "storage-sort-worker-cache"]);
        table.Find("[data-testid=storage-sort-total]").ParentElement!.GetAttribute("aria-sort").ShouldBe("descending");
        table.Find("[data-testid=storage-sort-stems]").ParentElement!.GetAttribute("aria-sort").ShouldBe("none");
        Order(table).ShouldBe([$"storage-track-row-{StorageData.TrackA}", $"storage-track-row-{StorageData.TrackB}"]);
        table.Find("[role=region]").GetAttribute("tabindex").ShouldBe("0");
    }

    [Fact]
    public async Task Sorting_ByAColumn_ThenReversed()
    {
        var table = RenderTable();

        await table.Find("[data-testid=storage-sort-worker-cache]").ClickAsync(new());
        Order(table)[0].ShouldBe($"storage-track-row-{StorageData.TrackB}");
        table.Find("[data-testid=storage-sort-worker-cache]").ParentElement!.GetAttribute("aria-sort").ShouldBe("descending");
        table.Find("[data-testid=storage-sort-total]").ParentElement!.GetAttribute("aria-sort").ShouldBe("none");

        await table.Find("[data-testid=storage-sort-worker-cache]").ClickAsync(new());
        table.Find("[data-testid=storage-sort-worker-cache]").ParentElement!.GetAttribute("aria-sort").ShouldBe("ascending");
        Order(table)[0].ShouldBe($"storage-track-row-{StorageData.TrackB}");

        await table.Find("[data-testid=storage-sort-total]").ClickAsync(new());
        await table.Find("[data-testid=storage-sort-total]").ClickAsync(new());
        Order(table)[0].ShouldBe($"storage-track-row-{StorageData.TrackB}");
    }

    [Fact]
    public void Rows_CarryTotalAndDetailState_AndLinkTheTrack()
    {
        var table = RenderTable();

        var row = table.Find($"[data-testid=storage-track-row-{StorageData.TrackB}]");
        (row.GetAttribute("data-total-bytes"), row.GetAttribute("data-detail-removed")).ShouldBe(((100 * StorageData.MiB).ToString(System.Globalization.CultureInfo.InvariantCulture), "true"));
        row.QuerySelector("a")!.GetAttribute("href").ShouldBe($"tracks/{StorageData.TrackB}");
        row.TextContent.ShouldContain("Band");
        table.Find($"[data-testid=storage-track-row-{StorageData.TrackA}]").GetAttribute("data-detail-removed").ShouldBe("false");
        table.Find($"[data-testid=storage-track-row-{StorageData.TrackA}]").TextContent.ShouldContain("Not measured");
        table.FindAll("thead th").Count.ShouldBe(11);
    }

    [Fact]
    public void Actions_AddAColumn()
    {
        var table = Render<StorageTracksTable>(p => p.Add(t => t.Tracks, [Big]).Add(t => t.Actions, track => b => b.AddContent(0, $"act-{track.Title}")));

        table.FindAll("thead th").Count.ShouldBe(12);
        table.Find("tbody tr").TextContent.ShouldContain("act-Big song");
    }

    [Fact]
    public void WithoutArtist_OnlyTheTitle()
    {
        var table = Render<StorageTracksTable>(p => p.Add(t => t.Tracks, [Big with { Artist = null }]));

        table.Find("tbody th").TextContent.Trim().ShouldBe("Big song");
    }
}
