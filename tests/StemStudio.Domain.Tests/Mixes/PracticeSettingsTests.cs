using StemStudio.Domain.Mixes;

namespace StemStudio.Domain.Tests.Mixes;

/// <summary>Practice settings of a mix (feature 012, data-model.md 2.1 and 2.2, FR-014).</summary>
public sealed class PracticeSettingsTests
{
    [Fact]
    public void Defaults_MatchTheDataModel()
    {
        var practice = PracticeSettings.Default;

        (practice.SpeedPercent, practice.TransposeSemitones).ShouldBe((100, 0));
        practice.Trainer.ShouldBe(new SpeedTrainerSettings { Enabled = false, StartPercent = 70, StepPercent = 5, TargetPercent = 100, PassesPerStep = 1 });
        practice.CountIn.ShouldBe(new CountInSettings { Bars = 0, EveryPass = false });
        new PracticeSettings().ShouldBe(practice);
        SpeedTrainerSettings.Default.ShouldBe(practice.Trainer);
        CountInSettings.Default.ShouldBe(practice.CountIn);
    }

    [Fact]
    public void MixSettings_DefaultsToTheNeutralPractice()
    {
        MixTests.Settings().Practice.ShouldBe(PracticeSettings.Default);
    }

    [Fact]
    public void IsNeutral_OnlyAtFullSpeedOriginalKeyAndTrainerOff()
    {
        PracticeSettings.Default.IsNeutral.ShouldBeTrue();
        (PracticeSettings.Default with { CountIn = new CountInSettings { Bars = 2, EveryPass = true } }).IsNeutral.ShouldBeTrue();
        (PracticeSettings.Default with { SpeedPercent = 99 }).IsNeutral.ShouldBeFalse();
        (PracticeSettings.Default with { TransposeSemitones = -1 }).IsNeutral.ShouldBeFalse();
        (PracticeSettings.Default with { Trainer = SpeedTrainerSettings.Default with { Enabled = true } }).IsNeutral.ShouldBeFalse();
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(120)]
    public void Validate_AcceptsSpeedWithinRange(int speed)
    {
        FieldsOf(PracticeSettings.Default with { SpeedPercent = speed }).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(49)]
    [InlineData(121)]
    [InlineData(0)]
    public void Validate_RejectsSpeedOutOfRange(int speed)
    {
        ErrorsOf(PracticeSettings.Default with { SpeedPercent = speed })
            .ShouldBe([new DomainError("practice.speedPercent", "Must be between 50 and 120.")]);
    }

    [Theory]
    [InlineData(-12, true)]
    [InlineData(12, true)]
    [InlineData(0, true)]
    [InlineData(-13, false)]
    [InlineData(13, false)]
    public void Validate_ChecksTransposeRange(int semitones, bool valid)
    {
        var fields = FieldsOf(PracticeSettings.Default with { TransposeSemitones = semitones });

        fields.ShouldBe(valid ? [] : ["practice.transposeSemitones"]);
    }

    [Theory]
    [InlineData(50, 120, true)]
    [InlineData(119, 120, true)]
    [InlineData(49, 100, false)]
    [InlineData(120, 120, false)]
    public void Validate_ChecksTrainerStartRange(int start, int target, bool valid)
    {
        var fields = FieldsOf(WithTrainer(t => t with { StartPercent = start, TargetPercent = target }));

        if (valid)
        {
            fields.ShouldBeEmpty();
        }
        else
        {
            fields.ShouldContain("practice.trainer.startPercent");
        }
    }

    [Theory]
    [InlineData(50, 51, true)]
    [InlineData(50, 120, true)]
    [InlineData(50, 50, false)]
    [InlineData(50, 121, false)]
    public void Validate_ChecksTrainerTargetRange(int start, int target, bool valid)
    {
        var fields = FieldsOf(WithTrainer(t => t with { StartPercent = start, TargetPercent = target }));

        fields.Contains("practice.trainer.targetPercent").ShouldBe(!valid);
        fields.ShouldNotContain("practice.trainer.startPercent");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(0, false)]
    [InlineData(21, false)]
    public void Validate_ChecksTrainerStepRange(int step, bool valid)
    {
        FieldsOf(WithTrainer(t => t with { StepPercent = step })).ShouldBe(valid ? [] : ["practice.trainer.stepPercent"]);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(8, true)]
    [InlineData(0, false)]
    [InlineData(9, false)]
    public void Validate_ChecksPassesPerStepRange(int passes, bool valid)
    {
        FieldsOf(WithTrainer(t => t with { PassesPerStep = passes })).ShouldBe(valid ? [] : ["practice.trainer.passesPerStep"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_RequiresTargetAboveStart_AlsoWhenTheTrainerIsOff(bool enabled)
    {
        var errors = ErrorsOf(WithTrainer(t => t with { Enabled = enabled, StartPercent = 90, TargetPercent = 80 }));

        errors.ShouldBe([new DomainError("practice.trainer.targetPercent", PracticeSettings.TargetAboveStartMessage)]);
    }

    [Fact]
    public void Validate_EqualStartAndTarget_ReportsTheTargetRuleOnce()
    {
        var errors = ErrorsOf(WithTrainer(t => t with { StartPercent = 80, TargetPercent = 80 }));

        errors.ShouldBe([new DomainError("practice.trainer.targetPercent", PracticeSettings.TargetAboveStartMessage)]);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(-1, false)]
    [InlineData(3, false)]
    public void Validate_ChecksCountInBars(int bars, bool valid)
    {
        var practice = PracticeSettings.Default with { CountIn = new CountInSettings { Bars = bars, EveryPass = true } };

        FieldsOf(practice).ShouldBe(valid ? [] : ["practice.countIn.bars"]);
    }

    [Fact]
    public void Validate_TrainerAndEveryPassWithoutALoop_AreValid()
    {
        var practice = new PracticeSettings
        {
            SpeedPercent = 75,
            TransposeSemitones = -2,
            Trainer = new SpeedTrainerSettings { Enabled = true, StartPercent = 60, StepPercent = 5, TargetPercent = 100, PassesPerStep = 2 },
            CountIn = new CountInSettings { Bars = 1, EveryPass = true },
        };

        FieldsOf(practice).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ReportsEveryViolationWithItsPath()
    {
        var practice = new PracticeSettings
        {
            SpeedPercent = 10,
            TransposeSemitones = 20,
            Trainer = new SpeedTrainerSettings { Enabled = true, StartPercent = 10, StepPercent = 0, TargetPercent = 200, PassesPerStep = 0 },
            CountIn = new CountInSettings { Bars = 5, EveryPass = false },
        };

        FieldsOf(practice).ShouldBe(
        [
            "practice.speedPercent", "practice.transposeSemitones", "practice.trainer.startPercent", "practice.trainer.stepPercent",
            "practice.trainer.targetPercent", "practice.trainer.passesPerStep", "practice.countIn.bars",
        ]);
    }

    [Fact]
    public void Mix_StoresUpdatesAndDuplicatesThePractice()
    {
        var track = MixTests.CompletedTrack();
        var practice = PracticeSettings.Default with { SpeedPercent = 80, CountIn = new CountInSettings { Bars = 1, EveryPass = false } };
        var mix = Mix.Create(Guid.NewGuid(), track, MixTests.Settings() with { Practice = practice }, DateTime.UnixEpoch);

        var copy = mix.Duplicate(Guid.NewGuid(), [mix.Name], DateTime.UnixEpoch);
        mix.Update(track, MixTests.Settings(), DateTime.UnixEpoch);

        copy.Practice.ShouldBe(practice);
        copy.Settings.Practice.ShouldBe(practice);
        mix.Practice.ShouldBe(PracticeSettings.Default);
    }

    [Fact]
    public void Mix_InvalidPractice_IsRejectedAndTheMixUnchanged()
    {
        var track = MixTests.CompletedTrack();
        var mix = Mix.Create(Guid.NewGuid(), track, MixTests.Settings(), DateTime.UnixEpoch);

        var exception = Should.Throw<DomainValidationException>(() => mix.Update(track, MixTests.Settings() with { Practice = PracticeSettings.Default with { SpeedPercent = 200 } }, DateTime.UnixEpoch));

        exception.Errors.Select(e => e.Field).ShouldBe(["practice.speedPercent"]);
        mix.Practice.ShouldBe(PracticeSettings.Default);
    }

    private static PracticeSettings WithTrainer(Func<SpeedTrainerSettings, SpeedTrainerSettings> change) =>
        PracticeSettings.Default with { Trainer = change(SpeedTrainerSettings.Default) };

    private static List<DomainError> ErrorsOf(PracticeSettings practice) =>
        (MixTests.Settings() with { Practice = practice }).Validate(MixTests.CompletedTrack()).ToList();

    private static List<string> FieldsOf(PracticeSettings practice) => ErrorsOf(practice).Select(e => e.Field).ToList();
}
