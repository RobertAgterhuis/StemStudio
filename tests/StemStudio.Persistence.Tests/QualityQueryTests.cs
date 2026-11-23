using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Configurations;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>Security review 020: the stored quality report read past the tracked entity (finding 6).</summary>
public sealed class QualityQueryTests
{
    private const string QualityJobId = "0d1e2f3a4b5c4d6e8f9a0b1c2d3e4f5a";
    private static readonly DateTime T0 = TestData.T0;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task IsQualityActiveStored_ReadsTheDatabase_NotTheTrackedTrack()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (queued, failed, none) = await SeedQueuedFailedAndNoneAsync(db);

        await using var context = db.NewContext();
        var tracks = new TrackRepository(context);
        var tracked = (await tracks.FindAsync(none.Id, Ct))!;
        await using (var other = db.NewContext())
        {
            var elsewhere = new TrackRepository(other);
            Queue((await elsewhere.FindAsync(none.Id, Ct))!);
            await elsewhere.SaveAsync(Ct);
        }

        tracked.QualityReport.ShouldBeNull();
        (await tracks.IsQualityActiveStoredAsync(none.Id, Ct)).ShouldBeTrue();
        (await tracks.IsQualityActiveStoredAsync(queued.Id, Ct)).ShouldBeTrue();
        (await tracks.IsQualityActiveStoredAsync(failed.Id, Ct)).ShouldBeFalse();
        (await tracks.IsQualityActiveStoredAsync(Guid.NewGuid(), Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task ListQualityActive_LoadsOnlyTheActiveTracks()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (queued, failed, none) = await SeedQueuedFailedAndNoneAsync(db);

        await using var context = db.NewContext();
        var active = await new TrackRepository(context).ListQualityActiveAsync(Ct);

        active.Select(track => track.Id).ShouldBe([queued.Id]);
        context.ChangeTracker.Entries<Track>().Count().ShouldBe(1, "the status is filtered in SQL, not after loading every report");
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("{\"Status\":\"Queued\",\"Stems\":5}")]
    [InlineData("[]")]
    public async Task ACorruptStoredReport_ReadsAsNoReport_AndBreaksNoQuery(string corrupt)
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (queued, broken) = (TestData.CompletedTrack("aaaaaaaaaaa"), TestData.CompletedTrack("bbbbbbbbbbb"));
        Queue(queued);
        Queue(broken);
        await db.SeedAsync(queued, broken);
        var faultsBefore = StoredJsonFaults.Count;
        await db.ScalarAsync($"UPDATE Tracks SET QualityReport = '{corrupt.Replace("'", "''", StringComparison.Ordinal)}' WHERE SourceKey = 'youtube:bbbbbbbbbbb'");

        await using var context = db.NewContext();
        var tracks = new TrackRepository(context);

        (await tracks.ListQualityActiveAsync(Ct)).Select(track => track.Id).ShouldBe([queued.Id]);
        (await tracks.FindAsync(broken.Id, Ct))!.QualityReport.ShouldBeNull();
        (await tracks.ListAsync(Ct)).Count.ShouldBe(2);
        (await tracks.IsQualityActiveStoredAsync(broken.Id, Ct)).ShouldBeFalse();
        StoredJsonFaults.Count.ShouldBeGreaterThan(faultsBefore);
    }

    [Fact]
    public void AnUnreadableValue_IsReportedOnlyOnce()
    {
        StoredJsonFaults.Record("QualityReport");

        var claims = Enumerable.Range(0, 3).Select(_ => StoredJsonFaults.TryClaimReport(out var column) ? column : null).Where(column => column is not null).ToList();

        claims.Count.ShouldBeLessThanOrEqualTo(1, "another test may have claimed the report first");
        StoredJsonFaults.TryClaimReport(out var name).ShouldBeFalse();
        name.ShouldNotBeEmpty();
    }

    private static async Task<(Track Queued, Track Failed, Track None)> SeedQueuedFailedAndNoneAsync(SqliteTestDatabase db)
    {
        var (queued, failed, none) = (TestData.CompletedTrack("aaaaaaaaaaa"), TestData.CompletedTrack("bbbbbbbbbbb"), TestData.CompletedTrack("ccccccccccc"));
        Queue(queued);
        Queue(failed);
        failed.FailQuality("broken", T0.AddMinutes(11));
        await db.SeedAsync(queued, failed, none);
        return (queued, failed, none);
    }

    private static void Queue(Track track)
    {
        track.RequestQuality("v1", T0.AddMinutes(10));
        track.AssignQualityJob(QualityJobId, T0.AddMinutes(10));
    }
}
