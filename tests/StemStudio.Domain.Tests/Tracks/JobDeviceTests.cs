using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;
using static StemStudio.Domain.Tests.Tracks.DetailTestData;

namespace StemStudio.Domain.Tests.Tracks;

/// <summary>Job device records on tracks and detail phases (feature 003, data-model.md 3.1; FR-006).</summary>
public sealed class JobDeviceTests
{
    private const string JobId = "3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c";
    private const string Gpu = "NVIDIA GeForce RTX 5070 Ti";
    private const string Oom = "The GPU ran out of memory during Mega 53-stem; the step was repeated on the CPU.";

    private static readonly JobDevice OnGpu = new(ProcessingDeviceKind.Gpu, Gpu, false, null);
    private static readonly JobDevice WithFallback = new(ProcessingDeviceKind.Gpu, Gpu, true, Oom);
    private static readonly JobDevice OnCpu = new(ProcessingDeviceKind.Cpu, null, false, null);

    [Fact]
    public void ValidDevices_KeepTheirValues()
    {
        (WithFallback.Kind, WithFallback.Name, WithFallback.CpuFallback, WithFallback.FallbackReason).ShouldBe((ProcessingDeviceKind.Gpu, Gpu, true, Oom));
        (OnCpu.Kind, OnCpu.Name, OnCpu.CpuFallback, OnCpu.FallbackReason).ShouldBe((ProcessingDeviceKind.Cpu, null, false, null));
        new JobDevice(ProcessingDeviceKind.Cpu, null, true, Oom).CpuFallback.ShouldBeTrue();
        new JobDevice(ProcessingDeviceKind.Gpu, new string('n', JobDevice.MaxNameLength), true, new string('r', JobDevice.MaxFallbackReasonLength)).ShouldNotBeNull();
    }

    [Fact]
    public void Devices_CompareByValue()
    {
        new JobDevice(ProcessingDeviceKind.Gpu, Gpu, false, null).ShouldBe(OnGpu);
        WithFallback.ShouldNotBe(OnGpu);
    }

    public static TheoryData<ProcessingDeviceKind, string?, bool, string?, string> InvalidDevices => new()
    {
        { ProcessingDeviceKind.Cpu, Gpu, false, null, "name" },
        { ProcessingDeviceKind.Gpu, new string('n', JobDevice.MaxNameLength + 1), false, null, "name" },
        { ProcessingDeviceKind.Gpu, Gpu, true, new string('r', JobDevice.MaxFallbackReasonLength + 1), "fallbackReason" },
        { ProcessingDeviceKind.Gpu, Gpu, true, null, "fallbackReason" },
        { ProcessingDeviceKind.Gpu, Gpu, false, Oom, "fallbackReason" },
        { (ProcessingDeviceKind)7, null, false, null, "kind" },
    };

    [Theory]
    [MemberData(nameof(InvalidDevices))]
    public void InvalidDevices_AreRejected(ProcessingDeviceKind kind, string? name, bool cpuFallback, string? reason, string field)
    {
        var error = Should.Throw<DomainValidationException>(() => new JobDevice(kind, name, cpuFallback, reason));

        error.Errors.Select(e => e.Field).ShouldContain(field);
    }

    [Fact]
    public void NewTrack_HasNoDevice()
    {
        Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:dQw4w9WgXcQ"), "https://youtu.be/dQw4w9WgXcQ", T0).Device.ShouldBeNull();
    }

    [Fact]
    public void ApplyDevice_SetsAndReplacesTheDeviceWhileTheTrackIsActive()
    {
        var track = ActiveTrack();

        track.ApplyDevice(OnGpu, T0.AddMinutes(1));
        track.Device.ShouldBe(OnGpu);
        track.UpdatedAt.ShouldBe(T0.AddMinutes(1));

        track.ApplyDevice(WithFallback, T0.AddMinutes(2));
        track.Device.ShouldBe(WithFallback);
        track.UpdatedAt.ShouldBe(T0.AddMinutes(2));
    }

    [Fact]
    public void ApplyDevice_NullClearsTheDevice()
    {
        var track = ActiveTrack();
        track.ApplyDevice(OnGpu, T0);

        track.ApplyDevice(null, T0.AddMinutes(1));

        track.Device.ShouldBeNull();
    }

    [Fact]
    public void ApplyDevice_TheSameDeviceDoesNotTouchUpdatedAt()
    {
        var track = ActiveTrack();
        track.ApplyDevice(OnGpu, T0.AddMinutes(1));

        track.ApplyDevice(new JobDevice(ProcessingDeviceKind.Gpu, Gpu, false, null), T0.AddMinutes(5));

        track.UpdatedAt.ShouldBe(T0.AddMinutes(1));
    }

    [Fact]
    public void Device_IsKeptAfterCompletionAndNeverChangedThen()
    {
        var track = ActiveTrack();
        track.ApplyDevice(WithFallback, T0);
        track.Complete(TrackTests.Completion(), T0.AddMinutes(5));

        track.ApplyDevice(OnCpu, T0.AddMinutes(6));
        track.ApplyDevice(null, T0.AddMinutes(7));

        track.Device.ShouldBe(WithFallback);
        track.UpdatedAt.ShouldBe(T0.AddMinutes(5));
    }

    [Fact]
    public void Retry_ClearsTheDevice()
    {
        var track = ActiveTrack();
        track.ApplyDevice(OnGpu, T0);
        track.Fail("boom", T0.AddMinutes(1));
        track.ApplyDevice(OnCpu, T0.AddMinutes(2));
        track.Device.ShouldBe(OnGpu);

        track.Retry(T0.AddMinutes(3));

        track.Device.ShouldBeNull();
    }

    [Fact]
    public void DetailDevice_IsNullForAFreshPhase()
    {
        Requested().DetailPhase!.Device.ShouldBeNull();
    }

    [Fact]
    public void ApplyDetailDevice_SetsTheDeviceOfTheActivePhase()
    {
        var track = Requested();
        track.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Running, "Splitting drums (1 of 3)", "drums", 12), T0.AddMinutes(2));

        track.ApplyDetailDevice(WithFallback, T0.AddMinutes(3));

        track.DetailPhase!.Device.ShouldBe(WithFallback);
        (track.DetailPhase.UpdatedAt, track.UpdatedAt).ShouldBe((T0.AddMinutes(3), T0.AddMinutes(3)));
        (track.DetailPhase.Status, track.DetailPhase.Progress).ShouldBe((DetailPhaseStatus.Running, 12));
    }

    [Fact]
    public void ApplyDetailDevice_TheSameDeviceDoesNotTouchUpdatedAt()
    {
        var track = Requested();
        track.ApplyDetailDevice(OnGpu, T0.AddMinutes(2));

        track.ApplyDetailDevice(OnGpu, T0.AddMinutes(9));

        track.DetailPhase!.UpdatedAt.ShouldBe(T0.AddMinutes(2));
    }

    [Fact]
    public void DetailDevice_IsKeptByCompletionFailureAndCancellation()
    {
        var completed = WithDetailDevice(track => track.CompleteDetail(Outcomes(), T0.AddMinutes(30)));
        var failed = WithDetailDevice(track => track.FailDetail("boom", T0.AddMinutes(30)));
        var cancelled = WithDetailDevice(track => track.CancelDetail(T0.AddMinutes(30)));

        completed.DetailPhase!.Device.ShouldBe(WithFallback);
        failed.DetailPhase!.Device.ShouldBe(WithFallback);
        cancelled.DetailPhase!.Device.ShouldBe(WithFallback);
    }

    [Fact]
    public void ApplyDetailDevice_IsIgnoredWithoutAnActivePhase()
    {
        var none = CompletedTrack();
        var completed = WithDetailDevice(track => track.CompleteDetail(Outcomes(), T0.AddMinutes(30)));

        none.ApplyDetailDevice(OnGpu, T0.AddHours(1));
        completed.ApplyDetailDevice(OnCpu, T0.AddHours(1));

        none.DetailPhase.ShouldBeNull();
        completed.DetailPhase!.Device.ShouldBe(WithFallback);
        completed.UpdatedAt.ShouldBe(T0.AddMinutes(30));
    }

    [Fact]
    public void RestartingTheDetailPhase_ClearsItsDevice()
    {
        var track = WithDetailDevice(t => t.FailDetail("boom", T0.AddMinutes(30)));

        track.RequestDetail(T0.AddHours(1));

        track.DetailPhase!.Device.ShouldBeNull();
    }

    [Fact]
    public void DetailDevice_NeverChangesTheStandardDevice()
    {
        var track = ActiveTrack();
        track.ApplyDevice(OnGpu, T0);
        track.Complete(TrackTests.Completion(), T0.AddMinutes(5));
        track.RequestDetail(T0.AddMinutes(6));

        track.ApplyDetailDevice(WithFallback, T0.AddMinutes(7));

        track.Device.ShouldBe(OnGpu);
    }

    [Fact]
    public void InvalidWorkerData_IsRejectedBeforeItReachesTheTrack()
    {
        var track = ActiveTrack();
        track.ApplyDevice(OnGpu, T0);

        Should.Throw<DomainValidationException>(() => track.ApplyDevice(new JobDevice(ProcessingDeviceKind.Cpu, Gpu, false, null), T0.AddMinutes(1)));

        (track.Device, track.Status, track.Error).ShouldBe((OnGpu, TrackStatus.Queued, null));
    }

    private static Track ActiveTrack()
    {
        var track = Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:dQw4w9WgXcQ"), "https://youtu.be/dQw4w9WgXcQ", T0);
        track.AssignJob(JobId, T0);
        return track;
    }

    private static Track WithDetailDevice(Action<Track> finish)
    {
        var track = Requested();
        track.ApplyDetailDevice(WithFallback, T0.AddMinutes(2));
        finish(track);
        return track;
    }
}
