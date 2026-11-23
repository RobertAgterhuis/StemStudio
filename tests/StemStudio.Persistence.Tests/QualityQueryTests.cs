using StemStudio.Domain.Tracks;
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
        var (queued, failed, none) = (TestData.CompletedTrack("aaaaaaaaaaa"), TestData.CompletedTrack("bbbbbbbbbbb"), TestData.CompletedTrack("ccccccccccc"));
        Queue(queued);
        Queue(failed);
        failed.FailQuality("broken", T0.AddMinutes(11));
        await db.SeedAsync(queued, failed, none);

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

    private static void Queue(Track track)
    {
        track.RequestQuality("v1", T0.AddMinutes(10));
        track.AssignQualityJob(QualityJobId, T0.AddMinutes(10));
    }
}
