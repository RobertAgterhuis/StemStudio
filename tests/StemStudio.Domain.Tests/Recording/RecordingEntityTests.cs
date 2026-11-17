using StemStudio.Domain.Assets;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Recording;

namespace StemStudio.Domain.Tests.Recording;

/// <summary>Feature 015 T022: the take and calibration entities, the take channel of a mix and the recording item.</summary>
public sealed class RecordingEntityTests
{
    public static readonly DateTime T0 = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);

    public static NewTake NewTake(string? name = null, RecordingPart part = RecordingPart.Bass) => new()
    {
        TrackId = Guid.NewGuid(),
        AssetId = Guid.NewGuid(),
        Number = 2,
        Name = name,
        Part = part,
        SessionId = Guid.NewGuid(),
        PassIndex = 3,
        Partial = true,
        StartSample = 44_100,
        LengthSamples = 88_200,
        InputMode = InputChannelMode.Mono1,
        InputChannels = 1,
        LatencySamples = 1102,
        LatencyMethod = LatencyMethod.Loopback,
        CalibrationId = Guid.NewGuid(),
        DeviceLabel = "USB\tAudio",
        MixId = Guid.NewGuid(),
        Scan = new PeakScan(-55, 2, false),
        Dropouts = 1,
        RecordedAt = T0.AddMinutes(-1),
    };

    [Fact]
    public void Create_CopiesTheValues_WithTheDefaultName()
    {
        var values = NewTake();

        var take = RecordingTake.Create(Guid.NewGuid(), values, T0);

        (take.Name, take.Number, take.Part, take.DeviceLabel, take.PeakDbfs, take.ClippedSamples, take.Quiet).ShouldBe(("Take 2 - Bass", 2, RecordingPart.Bass, "USBAudio", -55.0, 2, true));
        (take.TrackId, take.AssetId, take.SessionId, take.PassIndex, take.Partial).ShouldBe((values.TrackId, values.AssetId, values.SessionId, (int?)3, true));
        (take.StartSample, take.LengthSamples, take.InputMode, take.InputChannels).ShouldBe((44_100L, 88_200L, InputChannelMode.Mono1, 1));
        (take.LatencySamples, take.LatencyMethod, take.CalibrationId, take.MixId, take.Dropouts).ShouldBe((1102, LatencyMethod.Loopback, values.CalibrationId, values.MixId, 1));
        (take.Favorite, take.AdjustmentSamples, take.AlignmentRevision, take.RecordedAt, take.CreatedAt, take.UpdatedAt).ShouldBe((false, 0, 0, T0.AddMinutes(-1), T0, T0));
    }

    [Fact]
    public void Create_ValidatesAGivenName()
    {
        RecordingTake.Create(Guid.NewGuid(), NewTake(" Mine "), T0).Name.ShouldBe("Mine");
        Should.Throw<DomainValidationException>(() => RecordingTake.Create(Guid.NewGuid(), NewTake(""), T0));
        Should.Throw<ArgumentNullException>(() => RecordingTake.Create(Guid.NewGuid(), null!, T0));
    }

    [Fact]
    public void Edits_SetTheValuesAndTheTime()
    {
        var take = RecordingTake.Create(Guid.NewGuid(), NewTake(), T0);

        take.Rename(" Best ", T0.AddSeconds(1));
        take.SetFavorite(true, T0.AddSeconds(2));
        take.SetPart(RecordingPart.Vocals, T0.AddSeconds(3));
        take.DetachCalibration();
        take.DetachMix();

        (take.Name, take.Favorite, take.Part, take.CalibrationId, take.MixId, take.UpdatedAt).ShouldBe(("Best", true, RecordingPart.Vocals, (Guid?)null, (Guid?)null, T0.AddSeconds(3)));
    }

    [Fact]
    public void Realign_SumsTheAdjustment_AndCountsTheRevision()
    {
        var take = RecordingTake.Create(Guid.NewGuid(), NewTake(), T0);

        take.Realign(44_200, 88_100, 100, new PeakScan(-3, 0, false), T0.AddSeconds(1));
        take.Realign(44_150, 88_100, -50, new PeakScan(-3, 0, false), T0.AddSeconds(2));

        (take.StartSample, take.LengthSamples, take.AdjustmentSamples, take.AlignmentRevision, take.PeakDbfs, take.Quiet).ShouldBe((44_150L, 88_100L, 50, 2, -3.0, false));
        Should.Throw<ArgumentOutOfRangeException>(() => take.Realign(0, 22_050, 9_000, new PeakScan(-3, 0, false), T0));
        Should.Throw<ArgumentNullException>(() => take.Realign(0, 22_050, 0, null!, T0));
    }

    private static CalibrationValues Loopback() => new(" Focusrite ", "abc", 1102, CalibrationMethod.Loopback, 3, 8, 441);

    [Fact]
    public void Calibration_CreateAndReplace_TrimTheLabel()
    {
        var calibration = InputCalibration.Create(Guid.NewGuid(), Loopback(), T0);

        (calibration.DeviceLabel, calibration.DeviceIdHint, calibration.LatencySamples, calibration.Method, calibration.SpreadSamples, calibration.DetectedClicks, calibration.OutputLatencySamples, calibration.MeasuredAt)
            .ShouldBe(("Focusrite", "abc", 1102, CalibrationMethod.Loopback, (int?)3, (int?)8, 441, T0));
        calibration.Replace(new CalibrationValues("Focusrite", null, 900, CalibrationMethod.Manual, null, null, 0), T0.AddDays(1));
        (calibration.LatencySamples, calibration.Method, calibration.SpreadSamples, calibration.MeasuredAt).ShouldBe((900, CalibrationMethod.Manual, (int?)null, T0.AddDays(1)));
        InputCalibration.LabelKey(null).ShouldBe(string.Empty);
        Should.Throw<ArgumentNullException>(() => calibration.Replace(null!, T0));
    }

    public static TheoryData<CalibrationValues, string> InvalidCalibrations() => new()
    {
        { new CalibrationValues(" ", null, 0, CalibrationMethod.Manual, null, null, 0), "deviceLabel" },
        { new CalibrationValues(null, null, 0, CalibrationMethod.Manual, null, null, 0), "deviceLabel" },
        { new CalibrationValues(new string('a', 201), null, 0, CalibrationMethod.Manual, null, null, 0), "deviceLabel" },
        { new CalibrationValues("a\nb", null, 0, CalibrationMethod.Manual, null, null, 0), "deviceLabel" },
        { new CalibrationValues("a", new string('h', 513), 0, CalibrationMethod.Manual, null, null, 0), "deviceIdHint" },
        { new CalibrationValues("a", null, -1, CalibrationMethod.Manual, null, null, 0), "latencySamples" },
        { new CalibrationValues("a", null, 44_101, CalibrationMethod.Manual, null, null, 0), "latencySamples" },
        { new CalibrationValues("a", null, 0, CalibrationMethod.Manual, null, null, 44_101), "outputLatencySamples" },
        { new CalibrationValues("a", null, 0, (CalibrationMethod)9, null, null, 0), "method" },
        { new CalibrationValues("a", null, 0, CalibrationMethod.Manual, 2, null, 0), "spreadSamples" },
        { new CalibrationValues("a", null, 0, CalibrationMethod.Loopback, 45, null, 0), "spreadSamples" },
        { new CalibrationValues("a", null, 0, CalibrationMethod.Manual, null, 8, 0), "detectedClicks" },
        { new CalibrationValues("a", null, 0, CalibrationMethod.Loopback, null, 5, 0), "detectedClicks" },
        { new CalibrationValues("a", null, 0, CalibrationMethod.Loopback, null, 13, 0), "detectedClicks" },
    };

    [Theory]
    [MemberData(nameof(InvalidCalibrations))]
    public void Calibration_Validate_NamesTheField(CalibrationValues values, string field)
    {
        values.Validate().Select(error => error.Field).ShouldBe([field]);
        Should.Throw<DomainValidationException>(() => InputCalibration.Create(Guid.NewGuid(), values, T0));
    }

    [Fact]
    public void TakeChannel_DefaultsToUnity_AndIsValidatedWithTheMix()
    {
        var take = new TakeChannelSettings { TakeId = Guid.NewGuid() };
        (take.Volume, take.Pan, take.Mute, take.Solo).ShouldBe((1.0, 0.0, false, false));

        var track = Tests.Mixes.MixTests.CompletedTrack();
        var settings = Tests.Mixes.MixTests.Settings() with { Take = take with { Volume = 2, Pan = -3 } };

        settings.Validate(track).Select(error => error.Field).ShouldBe(["take.volume", "take.pan"]);
    }

    [Fact]
    public void Mix_KeepsTheTake_CopiesIt_AndRemovesItForItsTake()
    {
        var track = Tests.Mixes.MixTests.CompletedTrack();
        var take = new TakeChannelSettings { TakeId = Guid.NewGuid(), Volume = 0.5, Solo = true };
        var mix = Mix.Create(Guid.NewGuid(), track, Tests.Mixes.MixTests.Settings() with { Take = take }, T0);

        mix.Take.ShouldBe(take);
        mix.Settings.Take.ShouldBe(take);
        mix.Duplicate(Guid.NewGuid(), [], T0).Take.ShouldBe(take);
        mix.RemoveTake(Guid.NewGuid()).ShouldBeFalse();
        mix.RemoveTake(take.TakeId).ShouldBeTrue();
        mix.Take.ShouldBeNull();
        mix.UpdatedAt.ShouldBe(T0);
    }

    [Fact]
    public void RecordingAsset_NeedsItsTake_AndRerendersKeepingItsFiles()
    {
        var provenance = new Provenance
        {
            Origin = AssetOrigin.Recording,
            TrackId = Guid.NewGuid(),
            TrackTitle = "Song",
            Contents = "Take 1 (recording)",
            Recording = new RecordingSource(Guid.NewGuid()),
        };

        var asset = Asset.CreateRecording(Guid.NewGuid(), "Take 1", provenance, T0);
        Should.Throw<InvalidOperationException>(() => asset.Rerender(T0));
        asset.Complete([new AssetFile("audio", "audio.flac", "audio/flac", 10, new string('a', 64))], new AudioFacts(StemStudio.Domain.Rendering.AudioFormat.Flac, 24, null, 1, -3, 0), T0);
        asset.Rerender(T0.AddSeconds(5));

        (asset.Status, asset.Progress, asset.Files.Count, asset.UpdatedAt).ShouldBe((AssetStatus.Queued, 0, 1, T0.AddSeconds(5)));
        RecordingSource.CaptureFile.ShouldBe("capture.wav");
        Should.Throw<ArgumentException>(() => Asset.CreateRecording(Guid.NewGuid(), "x", provenance with { Recording = null }, T0));
        Should.Throw<ArgumentException>(() => Asset.CreateRecording(Guid.NewGuid(), "x", provenance with { Origin = AssetOrigin.Export }, T0));
    }

    [Fact]
    public void RecordingItems_AreNeverFiled()
    {
        var provenance = new Provenance { Origin = AssetOrigin.Recording, TrackTitle = "Song", Contents = "Take 1 (recording)", Recording = new RecordingSource(Guid.NewGuid()) };

        StemStudio.Domain.Library.LibraryFilingRules.IsFileable(provenance).ShouldBeFalse();
        StemStudio.Domain.Library.LibraryFilingRules.IsFileable(provenance with { Origin = AssetOrigin.Export }).ShouldBeTrue();
        StemStudio.Domain.Library.LibraryFilingRules.NeedsFiling(Asset.CreateRecording(Guid.NewGuid(), "Take 1", provenance, T0)).ShouldBeFalse();
    }
}
