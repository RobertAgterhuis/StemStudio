using StemStudio.Domain.Generations;
using StemStudio.Domain.Tracks;
using static StemStudio.Domain.Tests.Generations.GenerationTestData;

namespace StemStudio.Domain.Tests.Generations;

public sealed class GenerationTests
{
    private static readonly JobDevice Gpu = new(ProcessingDeviceKind.Gpu, "RTX", false, null);

    [Fact]
    public void CreateRestyle_IsQueued()
    {
        var generation = Queued();

        generation.Kind.ShouldBe(GenerationKind.Restyle);
        generation.Status.ShouldBe(GenerationState.Queued);
        generation.Stage.ShouldBe("Queued");
        generation.Progress.ShouldBe(0);
        generation.Seeds.ShouldBe([1000, 2000]);
        generation.Restyle.ShouldBe(Details());
        generation.Takes.ShouldBeEmpty();
        generation.Warnings.ShouldBeEmpty();
        generation.IsActive.ShouldBeTrue();
        generation.IsTerminal.ShouldBeFalse();
        generation.CanRetry.ShouldBeFalse();
        generation.SourceTrackId.ShouldBe(TrackId);
        generation.CreatedAt.ShouldBe(T0);
        generation.CompletedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void CreateRestyle_NeedsOneOrTwoSeeds(int count) =>
        Should.Throw<ArgumentException>(() => Queued(count));

    [Fact]
    public void CreateRestyle_RejectsNegativeSeeds() =>
        Should.Throw<ArgumentException>(() => Generation.CreateRestyle(Guid.NewGuid(), Details(), [-1], T0));

    [Fact]
    public void AssignJob_RecordsTheGeneratorJob()
    {
        var generation = Queued();

        generation.AssignJob(JobId, T0.AddSeconds(1));

        generation.GeneratorJobId.ShouldBe(JobId);
        generation.UpdatedAt.ShouldBe(T0.AddSeconds(1));
        Should.Throw<ArgumentException>(() => generation.AssignJob("bad", T0));
    }

    [Fact]
    public void ApplyReport_MirrorsTheGeneratorStates()
    {
        var generation = Queued();

        generation.ApplyReport(new GenerationReport(GenerationState.WaitingForGpu, 0, "Waiting for the GPU", null, []), T0.AddSeconds(5));
        generation.Status.ShouldBe(GenerationState.WaitingForGpu);
        generation.ApplyReport(new GenerationReport(GenerationState.Running, 40, "Generating take 1 of 2", Gpu, ["Clipped peaks"]), T0.AddSeconds(10));
        generation.ApplyReport(new GenerationReport(GenerationState.Running, 30, "Generating take 1 of 2", null, []), T0.AddSeconds(11));

        generation.Status.ShouldBe(GenerationState.Running);
        generation.Progress.ShouldBe(40);
        generation.Device.ShouldBe(Gpu);
        generation.Warnings.ShouldBe(["Clipped peaks"]);
        generation.Stage.ShouldBe("Generating take 1 of 2");
    }

    [Fact]
    public void ApplyReport_NewStatusTakesItsProgressAndClamps()
    {
        var generation = Queued();
        generation.ApplyReport(new GenerationReport(GenerationState.DownloadingModel, 80, null, null, []), T0);

        generation.ApplyReport(new GenerationReport(GenerationState.Running, 150, new string('s', 300), null, Enumerable.Repeat("w", 9).ToList()), T0);

        generation.Progress.ShouldBe(100);
        generation.Stage!.Length.ShouldBe(Generation.MaxStageLength);
        generation.Warnings.Count.ShouldBe(5);
    }

    [Fact]
    public void ApplyReport_IsIgnoredWhenTerminalAndRejectsTerminalStates()
    {
        var generation = Completed();

        generation.ApplyReport(new GenerationReport(GenerationState.Running, 10, "x", null, []), T0.AddMinutes(5));

        generation.Status.ShouldBe(GenerationState.Completed);
        Should.Throw<ArgumentException>(() => Queued().ApplyReport(new GenerationReport(GenerationState.Completed, 100, null, null, []), T0));
    }

    [Fact]
    public void Complete_StoresTheTakes()
    {
        var generation = Completed();

        generation.Status.ShouldBe(GenerationState.Completed);
        generation.Progress.ShouldBe(100);
        generation.Stage.ShouldBe("Completed");
        generation.CompletedAt.ShouldBe(T0.AddSeconds(40));
        generation.Takes.Select(t => t.Index).ShouldBe([1, 2]);
        generation.Takes.ShouldAllBe(t => t.AiGenerated);
        generation.IsTerminal.ShouldBeTrue();
        generation.Take(2)!.Seed.ShouldBe(2000);
        generation.Take(3).ShouldBeNull();
    }

    [Fact]
    public void Complete_RequiresTakesAndAnActiveGeneration()
    {
        Should.Throw<ArgumentException>(() => Queued().Complete([], T0));
        Should.Throw<InvalidOperationException>(() => Completed().Complete([Take(1)], T0));
    }

    [Fact]
    public void Fail_KeepsAReadableError()
    {
        var generation = Queued();

        generation.Fail(new GenerationError(GenerationErrorCode.OutOfMemory, new string('m', 600)), T0.AddSeconds(52));

        generation.Status.ShouldBe(GenerationState.Failed);
        generation.Stage.ShouldBe("Failed");
        generation.Error!.Code.ShouldBe(GenerationErrorCode.OutOfMemory);
        generation.Error.Message.Length.ShouldBe(500);
        generation.CanRetry.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => generation.Fail(new GenerationError(GenerationErrorCode.EngineError, "x"), T0));
    }

    [Fact]
    public void Fail_BlankMessageGetsADefault()
    {
        var generation = Queued();

        generation.Fail(new GenerationError(GenerationErrorCode.EngineError, " "), T0);

        generation.Error!.Message.ShouldBe(Generation.FailedDefault);
    }

    [Fact]
    public void Cancel_ActiveOnly()
    {
        var generation = Queued();

        generation.Cancel(T0.AddSeconds(3)).ShouldBeTrue();
        generation.Status.ShouldBe(GenerationState.Cancelled);
        generation.Stage.ShouldBe("Cancelled");
        generation.CanRetry.ShouldBeTrue();
        generation.Cancel(T0.AddSeconds(4)).ShouldBeFalse();
        Completed().Cancel(T0).ShouldBeFalse();
    }

    [Fact]
    public void Retry_CreatesANewGenerationWithNewSeeds()
    {
        var generation = Queued();
        generation.Fail(new GenerationError(GenerationErrorCode.Interrupted, "Interrupted"), T0);
        var id = Guid.NewGuid();

        var retry = generation.Retry(id, [7, 8], T0.AddMinutes(1));

        retry.Id.ShouldBe(id);
        retry.Status.ShouldBe(GenerationState.Queued);
        retry.Restyle.ShouldBe(generation.Restyle);
        retry.Seeds.ShouldBe([7, 8]);
        Should.Throw<InvalidOperationException>(() => Completed().Retry(Guid.NewGuid(), [1], T0));
    }

    [Fact]
    public void DetachSourceTrack_KeepsTheTitleSnapshot()
    {
        var generation = Completed();

        generation.DetachSourceTrack(Guid.NewGuid()).ShouldBeFalse();
        generation.DetachSourceTrack(TrackId).ShouldBeTrue();

        generation.SourceTrackId.ShouldBeNull();
        generation.Restyle!.SourceTrackTitle.ShouldBe("Never Gonna Give You Up");
    }

    [Fact]
    public void Takes_RecordLibraryItemsAndImports()
    {
        var generation = Completed();
        var asset = Guid.NewGuid();
        var track = Guid.NewGuid();

        generation.SetLibraryAsset(1, asset, T0.AddMinutes(1));
        generation.MarkImported(2, track, T0.AddMinutes(2));

        generation.Take(1)!.LibraryAssetId.ShouldBe(asset);
        generation.Take(2)!.ImportedTrackId.ShouldBe(track);
        generation.UpdatedAt.ShouldBe(T0.AddMinutes(2));
        Should.Throw<ArgumentOutOfRangeException>(() => generation.MarkImported(3, track, T0));
    }

    [Fact]
    public void LibraryAssetIds_ListsTheItems()
    {
        var generation = Completed();
        generation.SetLibraryAsset(1, Guid.NewGuid(), T0);

        generation.LibraryAssetIds.Count.ShouldBe(1);
    }
}
