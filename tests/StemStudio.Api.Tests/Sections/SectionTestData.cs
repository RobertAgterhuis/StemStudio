using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.SongSections;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Tests.Worker;
using StemStudio.Api.Worker;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.SongSections;

/// <summary>Helpers of the 014 section tests.</summary>
internal static class SectionTestData
{
    public static Task<bool> SyncSectionsAsync(this ApiFactory factory) => factory.WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<SectionJobApplier>().SyncAllAsync(CancellationToken.None);
        return true;
    });

    public static Task<TrackSections?> FindSectionsAsync(this ApiFactory factory, Guid trackId) =>
        factory.WithScopeAsync(sp => sp.GetRequiredService<ISectionRepository>().FindAsync(trackId, CancellationToken.None));

    /// <summary>Stores a ready, detected set of the <c>section-completed-job</c> example (5 sections up to 64 s).</summary>
    public static Task<TrackSections> SeedDetectedAsync(this ApiFactory factory, Guid trackId) => factory.WithScopeAsync(async sp =>
    {
        var set = TrackSections.Create(trackId, SyncTestData.T0);
        var result = FakeWorkerPracticeClient.Example<WorkerSectionJob>("section-completed-job").Result!;
        set.ApplyDetected(SectionMapper.ToDetection(result), Guid.NewGuid, SyncTestData.T0);
        await sp.GetRequiredService<ISectionRepository>().AddAsync(set, CancellationToken.None);
        return set;
    });

    /// <summary>Lets the fake worker complete every separation job (default stems of a six-stem result).</summary>
    public static void CompleteSeparations(this ApiFactory factory) =>
        factory.Worker.OnGetJob = jobId => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(jobId, WorkerJobState.Completed));

    public static Track Completed(string videoId = "dQw4w9WgXcQ") => SyncTestData.CompletedTrack(videoId);
}
