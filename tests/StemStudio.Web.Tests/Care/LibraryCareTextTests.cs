using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.LibraryCare;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Care;

/// <summary>Texts, test-id parts and form rules of library care (013; ui-test-ids 1.12.0 "Template parts").</summary>
public sealed class LibraryCareTextTests
{
    [Theory]
    [InlineData(StorageCategoryDto.DetailStems, "detail-stems")]
    [InlineData(StorageCategoryDto.PendingUploads, "pending-uploads")]
    [InlineData(StorageCategoryDto.VoiceProfiles, "voice-profiles")]
    [InlineData(StorageCategoryDto.WorkerSeparationCache, "worker-separation-cache")]
    [InlineData(StorageCategoryDto.Stems, "stems")]
    public void Kebab_FollowsTheTemplateParts(StorageCategoryDto category, string id) => CareText.Kebab(category).ShouldBe(id);

    [Fact]
    public void Category_NamesEveryValue_AndFallsBackToTheWireName()
    {
        foreach (var category in Enum.GetValues<StorageCategoryDto>())
        {
            CareText.Category(category).ShouldNotBe(Web.Services.WireName.Of(category), category.ToString());
        }

        CareText.Category((BackupCategoryDto)99).ShouldBe("99");
    }

    [Fact]
    public void Counts_LeaveOutZeros_AndSayNothingWhenEmpty()
    {
        CareText.Counts(new ArchiveCountsDto { Tracks = 1, Mixes = 2, Assets = 0, Generations = 0, Styles = 1, LyricsSheets = 2, VoiceProfiles = 0 })
            .ShouldBe("1 track, 2 mixes, 1 style, 2 lyrics sheets");
        CareText.Counts(new ArchiveCountsDto { Tracks = 0, Mixes = 0, Assets = 0, Generations = 0, Styles = 0, LyricsSheets = 0, VoiceProfiles = 0 })
            .ShouldBe("Nothing");
    }

    [Fact]
    public void ArchiveStatus_HasDefaultReasons()
    {
        CareText.ArchiveStatus(LibraryCareData.Summary(recognised: false) with { Reason = null })
            .ShouldBe("Not a StemStudio backup: the file has no readable contents list");
        CareText.ArchiveStatus(LibraryCareData.Summary(compatibility: LibraryCareData.Compatible(false)))
            .ShouldBe("Not compatible: made by a newer StemStudio");
    }

    [Fact]
    public void Eta_IsNullWhenUnknown() => CareText.Eta(null).ShouldBeNull();

    [Theory]
    [InlineData(LibraryJobStatusDto.Failed, "Failed")]
    [InlineData(LibraryJobStatusDto.Cancelled, "Cancelled")]
    public void Stage_OfAJobWithoutStage_IsItsStatus(LibraryJobStatusDto status, string text) =>
        CareText.Stage(LibraryCareData.Job(status: status, stage: null)).ShouldBe(text);

    [Fact]
    public void BackupForm_IgnoresTheAlwaysIncludedCategories()
    {
        var form = new BackupForm();

        form.Set(BackupCategoryDto.Database, false);

        form.Includes(BackupCategoryDto.Database).ShouldBeTrue();
        form.Options.ShouldBe(new BackupOptionsDto());
        form.ShowsVoiceNotice(null).ShouldBeFalse();
        BackupForm.Label(BackupCategoryDto.Recordings).ShouldBe("Recordings (play-along takes)");
        BackupForm.Label(BackupCategoryDto.Scores).ShouldBe("Sheet music");
    }

    [Fact]
    public void ArchiveRules_RestoreAndVerifyBlockers()
    {
        ArchiveRules.RestoreBlocked(LibraryCareData.Summary(), false).ShouldBeNull();
        ArchiveRules.RestoreBlocked(LibraryCareData.Summary(), true).ShouldBe("Wait for the running library job to finish.");
        ArchiveRules.RestoreBlocked(LibraryCareData.Summary(compatibility: LibraryCareData.Compatible(migration: true)), false).ShouldBeNull();
        ArchiveRules.VerifyBlocked(LibraryCareData.Summary(compatibility: LibraryCareData.Compatible(false)), false).ShouldBeNull();
        ArchiveRules.VerifyBlocked(LibraryCareData.Summary(recognised: false), false).ShouldBe("This file is not a StemStudio backup.");
    }

    [Theory]
    [InlineData("tracks", "Track \"Song\": already in your library")]
    [InlineData("assets", "Library item \"Song\": already in your library")]
    [InlineData("practiceLoops", "Practice loop \"Song\": already in your library")]
    [InlineData("track", "Track \"Song\": already in your library")]
    [InlineData("class", "Class \"Song\": already in your library")]
    [InlineData("", "Item \"Song\": already in your library")]
    [InlineData("s", "S \"Song\": already in your library")]
    public void SkippedText_NamesTheCategory_AndFallsBackToReadableWords(string category, string text) =>
        JobView.SkippedText(new SkippedItemDto { Category = category, Id = "t1", Name = "Song", Reason = SkippedItemReasonDto.SameId }).ShouldBe(text);
}
