using StemStudio.Domain.Recording;

namespace StemStudio.Domain.Tests.Recording;

/// <summary>Feature 015 T022: latency conversion, take rules, naming and calibration rules (data-model 2.1, 2.4, 2.5).</summary>
public sealed class TakeRulesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(25, 1103)]
    [InlineData(24.99, 1102)]
    [InlineData(0.1, 4)]
    [InlineData(-0.1, -4)]
    [InlineData(-200, -8820)]
    [InlineData(1000, 44100)]
    public void ToSamples_UsesTheRoundingRule(double ms, long samples) => LatencyConversion.ToSamples(ms).ShouldBe(samples);

    [Theory]
    [InlineData(1102, 24.99)]
    [InlineData(1543, 34.99)]
    [InlineData(44100, 1000)]
    [InlineData(-4, -0.09)]
    public void ToMilliseconds_RoundsToHundredths(long samples, double ms) => LatencyConversion.ToMilliseconds(samples).ShouldBe(ms);

    [Fact]
    public void Seconds_AndTrackSamples()
    {
        LatencyConversion.ToSeconds(66_150).ShouldBe(1.5);
        LatencyConversion.TrackSamples(213.04).ShouldBe(9_395_064);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(199, true)]
    [InlineData(200, false)]
    public void CanAdd_Below200(int existing, bool allowed) => TakeRules.CanAdd(existing).ShouldBe(allowed);

    [Theory]
    [InlineData(1.0, 0, true)]
    [InlineData(0.9, 0, false)]
    [InlineData(1.0, 2, false)]
    public void Speed_Only100PercentWithoutTranspose(double speed, int transpose, bool allowed) =>
        TakeRules.IsSupportedSpeed(speed, transpose).ShouldBe(allowed);

    [Theory]
    [InlineData(0, 100_000, true)]
    [InlineData(900_000, 104_410, true)]
    [InlineData(900_000, 104_411, false)]
    [InlineData(-1, 30_000, false)]
    [InlineData(1_000_000, 30_000, false)]
    [InlineData(0, 0, false)]
    public void InsideTrack_AllowsTheTolerance(long start, long length, bool inside) =>
        TakeRules.IsInsideTrack(start, length, 1_000_000).ShouldBe(inside);

    [Fact]
    public void SpanErrors_NameTheFields()
    {
        TakeRules.SpanErrors(0, 22_050).ShouldBeEmpty();
        TakeRules.SpanErrors(-1, 22_049).Select(error => error.Field).ShouldBe(["startSample", "lengthSamples"]);
        TakeRules.SpanErrors(0, (15L * 60 * 44_100) + 1).Single().Field.ShouldBe("lengthSamples");
    }

    [Fact]
    public void Check_FollowsTheRuleOrder()
    {
        var ok = new TakeCheck(0, 1, 0, 0, 30_000, 30_000, 1_000_000);

        TakeRules.Check(ok).ShouldBe(TakeRefusal.None);
        TakeRules.Check(ok with { ExistingTakes = 200, Speed = 2 }).ShouldBe(TakeRefusal.LimitReached);
        TakeRules.Check(ok with { Speed = 0.5, WavFrames = 1 }).ShouldBe(TakeRefusal.Speed);
        TakeRules.Check(ok with { WavFrames = 29_999, StartSample = 5_000_000 }).ShouldBe(TakeRefusal.LengthMismatch);
        TakeRules.Check(ok with { StartSample = 999_000 }).ShouldBe(TakeRefusal.OutsideTrack);
        Should.Throw<ArgumentNullException>(() => TakeRules.Check(null!));
    }

    [Theory]
    [InlineData(8820, true)]
    [InlineData(-8820, true)]
    [InlineData(8821, false)]
    [InlineData(int.MinValue, false)]
    public void Adjustment_Within200Ms(int adjustment, bool valid) => TakeRules.IsValidAdjustment(adjustment).ShouldBe(valid);

    [Fact]
    public void NextNumber_IsMaxPlusOne()
    {
        TakeNaming.NextNumber([]).ShouldBe(1);
        TakeNaming.NextNumber([1, 3, 2]).ShouldBe(4);
        Should.Throw<ArgumentNullException>(() => TakeNaming.NextNumber(null!));
    }

    [Theory]
    [InlineData(RecordingPart.None, "Take 3")]
    [InlineData(RecordingPart.Piano, "Take 3 - Keys")]
    [InlineData(RecordingPart.Bass, "Take 3 - Bass")]
    [InlineData(RecordingPart.Other, "Take 3 - Other")]
    public void DefaultName_PerPart(RecordingPart part, string name)
    {
        TakeNaming.DefaultName(3, part).ShouldBe(name);
        TakeNaming.Contents(3, part).ShouldBe($"{name} (recording)");
    }

    [Fact]
    public void Validate_TrimsAndRefusesBadNames()
    {
        TakeNaming.Validate("  Solo  ").ShouldBe("Solo");
        Should.Throw<DomainValidationException>(() => TakeNaming.Validate("   ")).Errors.Single().Field.ShouldBe("name");
        Should.Throw<DomainValidationException>(() => TakeNaming.Validate(null));
        Should.Throw<DomainValidationException>(() => TakeNaming.Validate(new string('a', 81)));
        Should.Throw<DomainValidationException>(() => TakeNaming.Validate("a\u0007b"));
    }

    [Fact]
    public void CleanDeviceLabel_StripsControlCharactersAndCuts()
    {
        TakeNaming.CleanDeviceLabel("Focusrite\r\n USB").ShouldBe("Focusrite USB");
        TakeNaming.CleanDeviceLabel("\u0000").ShouldBeNull();
        TakeNaming.CleanDeviceLabel(null).ShouldBeNull();
        TakeNaming.CleanDeviceLabel(new string('x', 300))!.Length.ShouldBe(200);
    }
}
