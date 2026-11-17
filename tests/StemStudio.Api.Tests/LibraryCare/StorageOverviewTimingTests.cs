using System.Diagnostics;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>
/// 013 SC-006 timing: the overview of a 500-track library within 2 s. A wall-clock budget only means something when the
/// test does not share the CPU with the rest of the parallel suite, so it runs in a collection without parallelization
/// (flake round 022/023).
/// </summary>
[Collection(StorageOverviewTimingTests.Name)]
public sealed class StorageOverviewTimingTests : ApiTestBase
{
    public const string Name = "Storage overview timing (not parallel)";

    [Fact]
    public async Task FiveHundredTracks_AreMeasuredQuickly()
    {
        for (var i = 0; i < 500; i++)
        {
            await Factory.SeedAsync(SyncTestData.CompletedTrack($"t{i:D10}"));
        }

        var watch = Stopwatch.StartNew();
        using var response = await Client.GetAsync(ApiRoutes.StorageOverview(refresh: true), Ct);

        (await response.BodyAsync<StorageOverviewDto>()).Tracks.Count.ShouldBe(500);
        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }
}

/// <summary>Runs the wall-clock tests alone, after the parallel tests.</summary>
[CollectionDefinition(StorageOverviewTimingTests.Name, DisableParallelization = true)]
public sealed class StorageOverviewTimingDefinition;
