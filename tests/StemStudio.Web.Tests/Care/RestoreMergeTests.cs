using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Care;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Care;

/// <summary>Merge a backup into the library (013 T064, US5, FR-031; ui-test-ids 1.12.0 restore-mode-merge, library-job-warnings).</summary>
public sealed class RestoreMergeTests : PageTestContext
{
    public RestoreMergeTests()
    {
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview());
        LibraryCare.RestoreBackupAsync(Arg.Any<string>(), Arg.Any<RestoreOptionsDto>(), Arg.Any<CancellationToken>()).Returns(LibraryCareData.Job(LibraryJobKindDto.Restore));
    }

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level, LibraryCareStatusDto? status = null) =>
        RenderAtLevel<RestoreDialog>(level, p => p.Add(d => d.Archive, LibraryCareData.Summary()).Add(d => d.Status, status ?? LibraryCareData.Status()));

    private RestoreOptionsDto Sent() =>
        (RestoreOptionsDto)LibraryCare.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(ILibraryCareClient.RestoreBackupAsync)).GetArguments()[1]!;

    [Theory]
    [InlineData(ExperienceLevel.Beginner, 0)]
    [InlineData(ExperienceLevel.Advanced, 1)]
    [InlineData(ExperienceLevel.Expert, 1)]
    public void Merge_IsOfferedFromAdvanced(ExperienceLevel level, int count)
    {
        var dialog = RenderAt(level);

        dialog.FindAll("[data-testid=restore-mode-merge]").Count.ShouldBe(count);
        dialog.FindAll("[data-testid=restore-mode-replace]").Count.ShouldBe(1);
    }

    [Fact]
    public void Merge_IsNotOffered_OnAnEmptyLibrary()
    {
        RenderAt(ExperienceLevel.Expert, LibraryCareData.Status(empty: true)).FindAll("[data-testid=restore-mode-merge]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Merge_NeedsNoTypedWord_AndIsNotBlockedByAPreviousLibrary()
    {
        var dialog = RenderAt(ExperienceLevel.Advanced, LibraryCareData.Status(previous: LibraryCareData.Previous()));

        await dialog.Find("[data-testid=restore-mode-merge]").ChangeAsync(new() { Value = "on" });

        dialog.FindAll("[data-testid=restore-confirm-input]").ShouldBeEmpty();
        dialog.Markup.ShouldNotContain("discard the previous library first");
        await dialog.Find("[data-testid=restore-start]").ClickAsync(new());
        Sent().ShouldBe(new RestoreOptionsDto { Mode = RestoreModeDto.Merge, VoiceProfiles = true, Confirmation = null });
    }

    [Fact]
    public async Task SwitchingBackToReplace_AsksForTheWordAgain()
    {
        var dialog = RenderAt(ExperienceLevel.Advanced);

        await dialog.Find("[data-testid=restore-mode-merge]").ChangeAsync(new() { Value = "on" });
        await dialog.Find("[data-testid=restore-mode-replace]").ChangeAsync(new() { Value = "on" });

        dialog.Find("[data-testid=restore-confirm-input]").ShouldNotBeNull();
        dialog.Find("[data-testid=restore-start]").HasAttribute("disabled").ShouldBeTrue();
    }

    /// <summary>Skipped and renamed items as the merge restore reports them (backend-013-us5 "Contract readings") and their lines.</summary>
    private static readonly (SkippedItemDto Item, string Text)[] ReportedItems =
    [
        (Skip("tracks", "t1", "Song", SkippedItemReasonDto.SameId), "Track \"Song\": already in your library"),
        (Skip("tracks", "t2", null, SkippedItemReasonDto.SameSource), "Track t2: the same song is already in your library"),
        (Skip("mixes", "m1", "Practice", SkippedItemReasonDto.DependencySkipped), "Mix \"Practice\": the item it belongs to was skipped"),
        (Skip("mixes", "m2", "Groove", SkippedItemReasonDto.SameId) with { RenamedTo = "Groove (restored)" },
            "Mix \"Groove\" added as \"Groove (restored)\" (the name was taken)"),
        (Skip("styles", "s1", "Warm", SkippedItemReasonDto.SameId) with { RenamedTo = "Warm (restored)" },
            "Style \"Warm\" added as \"Warm (restored)\" (the name was taken)"),
        (Skip("lyricsSheets", "l1", "Verse", SkippedItemReasonDto.SameId), "Lyrics sheet \"Verse\": already in your library"),
        (Skip("voiceProfiles", "v1", "Voice", SkippedItemReasonDto.Option), "Voice profile \"Voice\": left out by your choice"),
        (Skip("assets", "a1", "Bounce", SkippedItemReasonDto.MissingFile), "Library item \"Bounce\": its file is missing from the archive"),
        (Skip("generations", "g1", null, SkippedItemReasonDto.SameId), "Generation g1: already in your library"),
        (Skip("agentConversations", "c1", "Chat", SkippedItemReasonDto.SameId), "Agent conversation \"Chat\": already in your library"),
    ];

    [Fact]
    public void MergeReport_ListsSkippedAndRenamedItems()
    {
        var report = new RestoreReportDto
        {
            Mode = RestoreReportModeDto.Merge,
            Restored = [new CategoryCountDto { Category = BackupCategoryDto.Stems, Items = 1, Files = 6, Bytes = 100 * StorageData.MiB }],
            Skipped = [.. ReportedItems.Select(r => r.Item)],
            MigratedFrom = null,
            Warnings = [],
            PreviousLibraryKept = false,
        };
        LibraryCare.ListJobsAsync(1, Arg.Any<CancellationToken>()).Returns([LibraryCareData.Completed(LibraryJobKindDto.Restore, report)]);

        var page = Render<LibraryCarePage>();

        page.Find("[data-testid=library-job-report]").TextContent.ShouldContain("The missing items of the backup were added to your library.");
        var warnings = page.FindAll("[data-testid=library-job-warnings] li").Select(li => li.TextContent.Trim()).ToList();
        warnings.ShouldBe(ReportedItems.Select(r => r.Text).ToList());
    }

    private static SkippedItemDto Skip(string category, string id, string? name, SkippedItemReasonDto reason) =>
        new() { Category = category, Id = id, Name = name, Reason = reason };
}
