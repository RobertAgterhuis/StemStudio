using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Components.Care;
using StemStudio.Web.Experience;
using StemStudio.Web.LibraryCare;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Care;

/// <summary>
/// The Storage tab (013 T056, US3, FR-020, FR-021, FR-031, FR-032; ui-test-ids 1.12.0 storage-*): areas as meters, parts
/// with their class, the per-track table (Advanced+) or the top five (Beginner), unattached items, model caches,
/// worker details (Expert), backups and the previous library.
/// </summary>
public sealed class StorageTabTests : PageTestContext
{
    public StorageTabTests() =>
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview());

    private IRenderedComponent<StorageTab> RenderTab(LibraryCareStatusDto? status = null) =>
        Render<StorageTab>(p => p.Add(t => t.Status, status ?? LibraryCareData.Status()));

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) =>
        RenderAtLevel<StorageTab>(level, p => p.Add(t => t.Status, LibraryCareData.Status()));

    private static string Id(string id) => $"[data-testid='{id}']";

    [Fact]
    public void Areas_SharedDisks_ShowOneMeterLine()
    {
        var tab = RenderTab();

        var library = tab.Find(Id("storage-volume-library"));
        (library.GetAttribute("role"), library.GetAttribute("aria-valuenow"), library.GetAttribute("aria-valuemin"), library.GetAttribute("aria-valuemax"))
            .ShouldBe(("meter", "75", "0", "100"));
        (library.GetAttribute("data-free-bytes"), library.GetAttribute("data-total-bytes")).ShouldBe(((100 * StorageData.GiB).ToString(System.Globalization.CultureInfo.InvariantCulture),
            (400 * StorageData.GiB).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        library.GetAttribute("data-shared-with").ShouldBe("worker-data,model-cache");
        library.GetAttribute("aria-label").ShouldBe("Library, Worker data and Model cache (same disk)");
        library.GetAttribute("aria-valuetext").ShouldBe("100 GB free of 400 GB");
        library.GetAttribute("data-help-id").ShouldBe("storage-volume-library");
        tab.FindAll(Id("storage-volume-worker-data")).ShouldBeEmpty();
        tab.FindAll(Id("storage-volume-model-cache")).ShouldBeEmpty();
        var backups = tab.Find(Id("storage-volume-backups"));
        (backups.HasAttribute("data-shared-with"), backups.GetAttribute("aria-label")).ShouldBe((false, "Backups folder"));
        tab.Find(Id("storage-measured-at")).TextContent.ShouldBe("Measured 17 Nov 2026, 10:20");
        tab.FindAll(Id("storage-worker-unavailable")).ShouldBeEmpty();
    }

    [Fact]
    public void WorkerUnavailable_ShowsTheReason_AndTheLibraryPartStays()
    {
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview(workerAvailable: false) with
        {
            Volumes = [StorageData.Volume(StorageVolumeIdDto.Library), StorageData.Volume(StorageVolumeIdDto.WorkerData, null, null) with { Reason = "worker unavailable" }],
        });

        var tab = RenderTab();

        tab.Find(Id("storage-worker-unavailable")).TextContent.ShouldBe("Worker part not available: The worker is not running.");
        var worker = tab.Find(Id("storage-volume-worker-data"));
        (worker.HasAttribute("role"), worker.HasAttribute("aria-valuenow"), worker.TextContent.Contains("Not available: worker unavailable", StringComparison.Ordinal))
            .ShouldBe((false, false, true));
        tab.Find(Id("storage-category-worker-separation-cache")).TextContent.ShouldContain("Not measured");
        tab.Find(Id("storage-category-stems")).GetAttribute("data-bytes").ShouldBe((800 * StorageData.MiB).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void WorkerUnavailable_WithoutReason_SaysItCannotBeReached()
    {
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview() with
        {
            Worker = new WorkerStorageStateDto { Available = false, Reason = null },
            Volumes = [StorageData.Volume(StorageVolumeIdDto.ModelCache, null, null)],
        });

        var tab = RenderTab();

        tab.Find(Id("storage-worker-unavailable")).TextContent.ShouldContain("the worker cannot be reached");
        tab.Find(Id("storage-volume-model-cache")).TextContent.ShouldContain("Not available: it cannot be measured");
    }

    [Fact]
    public void Parts_HaveTheirClass_HowToRegenerate_AndManageLinks()
    {
        var tab = RenderTab();

        var detail = tab.Find(Id("storage-category-detail-stems"));
        (detail.GetAttribute("data-class"), detail.GetAttribute("data-help-id")).ShouldBe(("regenerable", "storage-category-detail-stems"));
        detail.TextContent.ShouldContain("Regenerable");
        detail.TextContent.ShouldContain("Split further on the track page (minutes per track)");
        var stems = tab.Find(Id("storage-category-stems"));
        stems.GetAttribute("data-class").ShouldBe("original");
        tab.Find(Id("storage-manage-stems")).GetAttribute("href").ShouldBe("tracks");
        tab.Find(Id("storage-manage-exports")).GetAttribute("href").ShouldBe("library");
        tab.FindAll(Id("storage-manage-detail-stems")).ShouldBeEmpty();
        tab.Find(Id("storage-category-leftovers")).TextContent.ShouldContain("Not needed");
    }

    [Theory]
    [InlineData(StorageCategoryDto.Database, "tracks")]
    [InlineData(StorageCategoryDto.Artwork, "tracks")]
    [InlineData(StorageCategoryDto.PendingUploads, "tracks")]
    [InlineData(StorageCategoryDto.Recordings, "tracks")]
    [InlineData(StorageCategoryDto.Scores, "library")]
    [InlineData(StorageCategoryDto.Generations, "generations")]
    [InlineData(StorageCategoryDto.VoiceProfiles, "voices")]
    [InlineData(StorageCategoryDto.PreviousLibrary, "care")]
    [InlineData(StorageCategoryDto.RestoreStaging, null)]
    public void ManageLinks_PointToTheScreenWhereItemsAreDeleted(StorageCategoryDto category, string? href) =>
        StorageView.ManageLink(category).ShouldBe(href);

    [Fact]
    public void OriginalWithoutScreen_AndRegenerableWithoutText_HaveDefaults()
    {
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview() with
        {
            Categories =
            [
                StorageData.Category(StorageCategoryDto.RestoreStaging, 1, 1),
                StorageData.Category(StorageCategoryDto.WorkerScratch, 1, null) with { Regenerability = RegenerabilityDto.Regenerable },
            ],
        });

        var tab = RenderTab();

        tab.Find(Id("storage-category-restore-staging")).TextContent.ShouldContain("Only from a backup");
        var scratch = tab.Find(Id("storage-category-worker-scratch"));
        scratch.TextContent.ShouldContain("StemStudio makes it again when needed");
        scratch.TextContent.ShouldContain("-");
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner, 0, 1, 0)]
    [InlineData(ExperienceLevel.Advanced, 1, 0, 0)]
    [InlineData(ExperienceLevel.Expert, 1, 0, 1)]
    public void Levels_TableAtAdvanced_TopFiveAtBeginner_WorkerEntriesAtExpert(ExperienceLevel level, int table, int top, int entries)
    {
        var tab = RenderAt(level);

        tab.FindAll(Id("storage-tracks-table")).Count.ShouldBe(table);
        tab.FindAll(Id("storage-top-tracks")).Count.ShouldBe(top);
        tab.FindAll(Id("storage-worker-entries")).Count.ShouldBe(entries);
    }

    [Fact]
    public void TopTracks_AreTheFiveLargest_WithLinks()
    {
        var tracks = Enumerable.Range(1, 7).Select(i => StorageData.Track(Guid.NewGuid(), $"Song {i}", i * StorageData.MiB)).ToList();
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview(tracks: tracks));

        var items = RenderAt(ExperienceLevel.Beginner).FindAll($"{Id("storage-top-tracks")} li");

        items.Select(li => li.QuerySelector("a")!.TextContent).ShouldBe(["Song 7", "Song 6", "Song 5", "Song 4", "Song 3"]);
        items[0].TextContent.ShouldContain("7.0 MB");
        items[0].QuerySelector("a")!.GetAttribute("href").ShouldBe($"tracks/{tracks[6].TrackId}");
    }

    [Fact]
    public void WorkerEntries_ListOneRowPerCacheEntry()
    {
        var entries = RenderAt(ExperienceLevel.Expert).Find(Id("storage-worker-entries"));

        var rows = entries.QuerySelectorAll("tbody tr");
        rows.Length.ShouldBe(2);
        rows[0].QuerySelectorAll("th, td").Select(cell => cell.TextContent.Trim()).ShouldBe(["3f9a1c0e7b2d", "2.0 GB", "Yes", "No", "Big song"]);
        rows[1].QuerySelectorAll("th, td").Select(cell => cell.TextContent.Trim()).ShouldBe(["a1b2c3d4e5f6", "5.0 MB", "No", "Yes", "-"]);
        rows[0].QuerySelector("a")!.GetAttribute("href").ShouldBe($"tracks/{StorageData.TrackA}");
        entries.QuerySelectorAll("thead th").Select(cell => cell.GetAttribute("scope")).ShouldAllBe(scope => scope == "col");
    }

    [Fact]
    public void TextButtons_AccessibleNameContainsTheVisibleText()
    {
        var buttons = RenderAt(ExperienceLevel.Expert).FindAll("button.ss-text-button[aria-label]");

        buttons.ShouldNotBeEmpty();
        buttons.ShouldAllBe(button => button.GetAttribute("aria-label")!.Contains(button.TextContent.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WorkerEntries_WithoutEntries_SayThatThereAreNone()
    {
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview(workerAvailable: false));
        var tab = RenderAt(ExperienceLevel.Expert);

        tab.Find(Id("storage-worker-entries")).QuerySelector("tbody")!.TextContent.ShouldContain("No worker cache entries.");
    }

    [Fact]
    public void Unattached_ModelCaches_Backups_AndPreviousLibrary()
    {
        var tab = RenderTab(LibraryCareData.Status(previous: LibraryCareData.Previous()));

        tab.Find(Id("storage-unattached")).TextContent.ShouldBe(
            "Without a track: exports 10 MB, sheet music 1.0 MB, generations 30 MB, voice profiles 2.0 MB, 4 lyrics sheets.");
        var cache = tab.Find(Id("storage-model-cache-torch"));
        cache.TextContent.ShouldContain("3.0 GB (htdemucs_6s, htdemucs)");
        cache.TextContent.ShouldContain("Downloaded again at first use");
        cache.QuerySelectorAll("button").ShouldBeEmpty();
        tab.Find(Id("storage-backups")).TextContent.ShouldContain("2 archives, 4.0 GB.");
        tab.Find(Id("storage-previous-library")).TextContent.ShouldContain("5.0 GB");
        tab.Find($"{Id("storage-previous-library")} a").GetAttribute("href").ShouldBe("care");
    }

    [Fact]
    public void NoPreviousLibrary_NoRow_AndAnUnavailableBackupsFolderSaysWhy()
    {
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview() with
        {
            Backups = new BackupsStorageDto { Count = 0, Bytes = 0, FolderAvailable = false, Reason = "missing" },
            ModelCaches = [new ModelCacheUsageDto { Id = "hf", Bytes = 0, Models = [] }],
        });

        var tab = RenderTab();

        tab.FindAll(Id("storage-previous-library")).ShouldBeEmpty();
        tab.Find(Id("storage-backups")).TextContent.ShouldContain("(folder not available: missing).");
        tab.Find(Id("storage-model-cache-hf")).TextContent.ShouldNotContain("(");
    }

    [Fact]
    public async Task Refresh_BypassesTheCache()
    {
        var tab = RenderTab();

        await tab.Find(Id("storage-refresh")).ClickAsync(new());

        await LibraryCare.Received(1).GetStorageOverviewAsync(false, Arg.Any<CancellationToken>());
        await LibraryCare.Received(1).GetStorageOverviewAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Failure_ShowsTheError()
    {
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "Down"));

        var tab = RenderTab();

        tab.Find(Id("storage-error")).TextContent.ShouldBe("Down");
        tab.FindAll(Id("storage-measured-at")).ShouldBeEmpty();
    }

    [Fact]
    public void WhileLoading_ShowsASkeleton()
    {
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<StorageOverviewDto>().Task);

        var tab = RenderTab();

        tab.Find(Id("storage-refresh")).HasAttribute("disabled").ShouldBeTrue();
        tab.FindAll(Id("storage-volume-library")).ShouldBeEmpty();
    }
}
