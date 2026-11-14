using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Api.Importing;
using StemStudio.Api.Storage;
using StemStudio.Api.Sync;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Importing;

/// <summary>Race paths that cannot be timed reliably over HTTP: the unique index catches a concurrent insert.</summary>
public sealed class ImportServiceTests
{
    private readonly ITrackRepository _tracks = Substitute.For<ITrackRepository>();
    private readonly FakeWorkerClient _worker = new();
    private readonly IUploadStore _uploads = Substitute.For<IUploadStore>();
    private readonly FakeTimeProvider _clock = new(ApiFactory.Start);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ConcurrentInsert_ReturnsTheExistingTrackWithoutWorkerCall()
    {
        var winner = SyncTestData.UrlTrack();
        _tracks.FindByKeyAsync(Arg.Any<SourceKey>(), Arg.Any<CancellationToken>()).Returns(null, winner);
        _tracks.AddAsync(Arg.Any<Track>(), Arg.Any<CancellationToken>()).ThrowsAsync(new DuplicateKeyException());

        var result = await Service().ImportUrlAsync("https://youtu.be/dQw4w9WgXcQ", Ct);

        result.ShouldBeOfType<Ok<TrackDto>>().Value!.Id.ShouldBe(winner.Id);
        _worker.CreateCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ConcurrentInsertThenDeleted_Returns503()
    {
        _tracks.FindByKeyAsync(Arg.Any<SourceKey>(), Arg.Any<CancellationToken>()).Returns((Track?)null);
        _tracks.AddAsync(Arg.Any<Track>(), Arg.Any<CancellationToken>()).ThrowsAsync(new DuplicateKeyException());

        var result = await Service().ImportUrlAsync("https://youtu.be/dQw4w9WgXcQ", Ct);

        result.ShouldBeOfType<ProblemHttpResult>().StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task ConcurrentUploadInsert_KeepsTheSharedFile()
    {
        var sha = new string('c', 64);
        var winner = Track.FromUpload(Guid.NewGuid(), SourceKey.ForUpload(sha), new UploadedFile("a.mp3", sha + ".mp3"), SyncTestData.T0);
        _uploads.SaveAsync(Arg.Any<Stream>(), ".mp3", Arg.Any<CancellationToken>()).Returns(new StoredUpload(sha + ".mp3", sha, 3));
        _tracks.FindByKeyAsync(Arg.Any<SourceKey>(), Arg.Any<CancellationToken>()).Returns(null, winner);
        _tracks.AddAsync(Arg.Any<Track>(), Arg.Any<CancellationToken>()).ThrowsAsync(new DuplicateKeyException());

        var result = await Service().ImportUploadAsync(new MemoryStream([1, 2, 3]), "a.mp3", Ct);

        result.ShouldBeOfType<Ok<TrackDto>>().Value!.Id.ShouldBe(winner.Id);
        _uploads.DidNotReceive().Delete(Arg.Any<string>());
    }

    public static TheoryData<Exception> InterruptingFailures() =>
    [
        new OperationCanceledException("client disconnected"),
        new InvalidOperationException("bug"),
    ];

    [Theory]
    [MemberData(nameof(InterruptingFailures))]
    public async Task WorkerCallInterrupted_FailsTheInsertedTrackRetryablyAndRethrows(Exception failure)
    {
        Track? added = null;
        _tracks.FindByKeyAsync(Arg.Any<SourceKey>(), Arg.Any<CancellationToken>()).Returns((Track?)null);
        _tracks.AddAsync(Arg.Do<Track>(track => added = track), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _worker.OnCreateUrl = _ => throw failure;

        var thrown = await Record.ExceptionAsync(() => Service().ImportUrlAsync("https://youtu.be/dQw4w9WgXcQ", Ct));

        thrown.ShouldBeSameAs(failure);
        added.ShouldNotBeNull();
        added.Status.ShouldBe(TrackStatus.Failed);
        added.Error.ShouldBe(OrphanedImportSweeper.ImportInterrupted);
        added.CanRetry.ShouldBeTrue();
        await _tracks.Received(1).SaveAsync(CancellationToken.None);
        await _tracks.DidNotReceive().RemoveAsync(Arg.Any<Track>(), Arg.Any<CancellationToken>());
    }

    private ImportService Service() => new(_tracks, _worker, _uploads, _clock);
}
