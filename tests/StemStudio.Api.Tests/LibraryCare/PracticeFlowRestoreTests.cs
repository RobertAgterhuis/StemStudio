using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Restore;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Domain.Practice;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Setlists;
using StemStudio.Persistence;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>
/// 014 T107 (decided 2026-11-17, round decision D7): a merge restore brings the sections of every merged track; setlists and
/// practice sessions of the archive are not merged; score views are not archived (the scrub removes their rows, so a ready
/// score asset has no view after a restore and the view is made again).
/// </summary>
public sealed class PracticeFlowRestoreTests : ApiTestBase
{
    private readonly LibraryFixture _library;

    public PracticeFlowRestoreTests() => _library = new LibraryFixture(Factory);

    private string Staged => Path.Combine(Factory.DataDir, "library-care", "staging", "practice", "stemstudio.db");

    private StemStudioDbContext StagedContext() =>
        new(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite($"Data Source={Staged};Pooling=False").Options);

    private async Task SnapshotAsync()
    {
        await new DatabaseSnapshot(TimeProvider.System, NullLogger.Instance).CreateAsync($"Data Source={Path.Combine(Factory.DataDir, "stemstudio.db")}", Staged, Ct);
        await new BackupScrubber().ScrubAsync(Staged, [], LibraryFixture.T0, Ct);
    }

    private async Task SectionsAsync(Guid trackId)
    {
        var sections = TrackSections.Create(trackId, LibraryFixture.T0);
        sections.ApplyDetected(new DetectionResult("stems-novelty/1.0.0", null, [], [new DetectedSection(default, "Verse", 0, 30, null, null)]), Guid.NewGuid, LibraryFixture.T0);
        await _library.AddAsync(sections);
    }

    private Task<T> LiveAsync<T>(Func<StemStudioDbContext, Task<T>> action) => Factory.WithScopeAsync(sp => action(sp.GetRequiredService<StemStudioDbContext>()));

    [Fact]
    public async Task TheScrub_RemovesScoreViews_SoARestoredScoreAssetHasNone()
    {
        var track = await _library.TrackAsync(detail: false);
        var asset = await _library.ExportAsync(track);
        await _library.AddAsync(ScoreView.Create(asset.Id, track.Id, 0, LibraryFixture.T0));

        await SnapshotAsync();

        await using var staged = StagedContext();
        (await staged.ScoreViews.CountAsync(Ct), await staged.Assets.CountAsync(Ct)).ShouldBe((0, 1));
        (await LiveAsync(context => context.ScoreViews.CountAsync(Ct))).ShouldBe(1);
    }

    [Fact]
    public async Task Merge_BringsTheSectionsOfMergedTracks_ButNoSetlistsOrScoreViews()
    {
        var kept = await _library.TrackAsync("aaaaaaaaaaa", detail: false);
        var lost = await _library.TrackAsync("bbbbbbbbbbb", detail: false);
        await SectionsAsync(kept.Id);
        await SectionsAsync(lost.Id);
        await _library.AddAsync(Setlist.Create(Guid.NewGuid(), "Gig", null, LibraryFixture.T0));
        await SnapshotAsync();
        await LiveAsync(async context =>
        {
            context.Tracks.Remove(await context.Tracks.SingleAsync(track => track.Id == lost.Id, Ct));
            context.Setlists.RemoveRange(await context.Setlists.ToListAsync(Ct));
            await context.SaveChangesAsync(Ct);
            return true;
        });

        var plan = await LiveAsync(async live =>
        {
            await using var staged = StagedContext();
            return await new MergePlanner(staged, live).PlanAsync(Ct);
        });

        var sections = plan.Added.OfType<TrackSections>().ShouldHaveSingleItem();
        (sections.TrackId, sections.Sections.Count).ShouldBe((lost.Id, 1));
        plan.Added.OfType<Setlist>().ShouldBeEmpty();
        plan.Added.OfType<PracticeSession>().ShouldBeEmpty();
        plan.Added.OfType<ScoreView>().ShouldBeEmpty();
    }
}
