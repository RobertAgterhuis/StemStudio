using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Api.Mixes;
using StemStudio.Api.Tests.Mixes;
using StemStudio.Domain.Mixes;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>Mix repository stubs for races that cannot be timed over HTTP.</summary>
internal static class MixRepositoryStubs
{
    /// <summary>One stored mix whose copy name looks free, but the unique index rejects the insert (concurrent clash).</summary>
    /// <returns>The stored mix and the repository.</returns>
    public static (Mix Mix, IMixRepository Repository) ConcurrentNameClash()
    {
        var track = SyncTestData.CompletedTrack();
        var mix = Mix.Create(Guid.NewGuid(), track, MixMapper.ToDomain(MixEndpointsTests.Settings()), SyncTestData.T0);
        var mixes = Substitute.For<IMixRepository>();
        mixes.FindAsync(mix.Id, Arg.Any<CancellationToken>()).Returns(mix);
        mixes.NamesForTrackAsync(track.Id, Arg.Any<CancellationToken>()).Returns([]);
        mixes.AddAsync(Arg.Any<Mix>(), Arg.Any<CancellationToken>()).ThrowsAsync(new DuplicateKeyException());
        return (mix, mixes);
    }
}
