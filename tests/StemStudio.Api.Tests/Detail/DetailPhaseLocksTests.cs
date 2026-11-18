using StemStudio.Api.Detail;

namespace StemStudio.Api.Tests.Detail;

/// <summary>The per-track lock of the detail phase.</summary>
public sealed class DetailPhaseLocksTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SameTrack_RunsOneBodyAtATime_OtherTracksAreNotBlocked()
    {
        var locks = new DetailPhaseLocks();
        var track = Guid.NewGuid();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = locks.RunAsync(track, () => release.Task, Ct);

        var second = locks.RunAsync(track, () => Task.FromResult(true), Ct);
        var other = await locks.RunAsync(Guid.NewGuid(), () => Task.FromResult(true), Ct);

        (other, second.IsCompleted).ShouldBe((true, false));
        release.SetResult(true);
        (await first, await second).ShouldBe((true, true));
    }

    [Fact]
    public async Task ThrowingBody_ReleasesTheLock()
    {
        var locks = new DetailPhaseLocks();
        var track = Guid.NewGuid();

        await Should.ThrowAsync<InvalidOperationException>(() => locks.RunAsync<bool>(track, () => throw new InvalidOperationException(), Ct));

        (await locks.RunAsync(track, () => Task.FromResult(true), Ct)).ShouldBeTrue();
        await Should.ThrowAsync<ArgumentNullException>(() => locks.RunAsync<bool>(track, null!, Ct));
    }
}
