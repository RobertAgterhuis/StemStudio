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

    [Fact]
    public void MergeReport_ListsSkippedAndRenamedItems()
    {
        var report = new RestoreReportDto
        {
            Mode = RestoreReportModeDto.Merge,
            Restored = [new CategoryCountDto { Category = BackupCategoryDto.Stems, Items = 1, Files = 6, Bytes = 100 * StorageData.MiB }],
            Skipped =
            [
                new SkippedItemDto { Category = "track", Id = "t1", Name = "Song", Reason = SkippedItemReasonDto.SameId },
                new SkippedItemDto { Category = "track", Id = "t2", Name = null, Reason = SkippedItemReasonDto.SameSource },
                new SkippedItemDto { Category = "mix", Id = "m1", Name = "Practice", Reason = SkippedItemReasonDto.DependencySkipped },
                new SkippedItemDto { Category = "style", Id = "s1", Name = "Warm", Reason = SkippedItemReasonDto.SameId, RenamedTo = "Warm (restored)" },
                new SkippedItemDto { Category = "asset", Id = "a1", Name = "Bounce", Reason = SkippedItemReasonDto.MissingFile },
                new SkippedItemDto { Category = "voiceProfile", Id = "v1", Name = "Voice", Reason = SkippedItemReasonDto.Option },
            ],
            MigratedFrom = null,
            Warnings = [],
            PreviousLibraryKept = false,
        };
        LibraryCare.ListJobsAsync(1, Arg.Any<CancellationToken>()).Returns([LibraryCareData.Job(LibraryJobKindDto.Restore, LibraryJobStatusDto.Completed, null, 100, report, cancellable: false)]);

        var page = Render<LibraryCarePage>();

        page.Find("[data-testid=library-job-report]").TextContent.ShouldContain("The missing items of the backup were added to your library.");
        var warnings = page.Find("[data-testid=library-job-warnings]").TextContent;
        foreach (var text in new[]
        {
            "Track \"Song\" skipped: already in your library",
            "Track t2 skipped: the same song is already in your library",
            "Mix \"Practice\" skipped: the item it belongs to was skipped",
            "Style \"Warm\" added as \"Warm (restored)\" (the name was taken)",
            "Asset \"Bounce\" skipped: its file is missing from the archive",
            "VoiceProfile \"Voice\" skipped: left out by your choice",
        })
        {
            warnings.ShouldContain(text);
        }
    }
}
