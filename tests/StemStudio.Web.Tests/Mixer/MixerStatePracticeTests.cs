using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Audio;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>Practice tempo in <see cref="MixerState"/>, <see cref="PracticeRules"/> and <see cref="PracticeSender"/> (012 T031).</summary>
public sealed class MixerStatePracticeTests
{
    private static readonly PracticeSettingsDto Slowed = new(75, -2, new SpeedTrainerSettingsDto(true, 60, 5, 100, 2), new CountInSettingsDto(2, true));

    private readonly MixerState _state = new(TestData.Track());

    [Fact]
    public void NewMixer_HasTheNeutralDefaults_AndIsClean()
    {
        _state.Practice.ShouldBe(PracticeSettingsDto.Default);
        _state.LastCountInBars.ShouldBe(1);
        _state.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void ApplyMix_RestoresPractice_AndRemembersTheBarCount()
    {
        _state.ApplyMix(TestData.Mix() with { Settings = TestData.Settings() with { Practice = Slowed } });

        _state.Practice.ShouldBe(Slowed);
        _state.LastCountInBars.ShouldBe(2);
        _state.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void ApplyMix_OfAMixSavedBefore012_LoadsTheDefaults()
    {
        _state.SetSpeed(60);
        var old = JsonSerializer.Deserialize<MixDto>(TestData.MixJson, JsonSerializerOptions.Web)!;
        var nulled = old with { Settings = old.Settings with { Practice = null! } };

        _state.ApplyMix(old);
        _state.Practice.ShouldBe(PracticeSettingsDto.Default);
        _state.SetSpeed(60);
        _state.ApplyMix(nulled);
        _state.Practice.ShouldBe(PracticeSettingsDto.Default);
    }

    [Fact]
    public void ToSettings_IncludesPractice()
    {
        _state.SetSpeed(80);
        _state.SetTranspose(3);

        var practice = _state.ToSettings("Mine").Practice;

        (practice.SpeedPercent, practice.TransposeSemitones).ShouldBe((80, 3));
    }

    [Theory]
    [InlineData(70, 70)]
    [InlineData(10, 50)]
    [InlineData(500, 120)]
    public void SetSpeed_ClampsToTheRange_AndMarksDirty(int requested, int expected)
    {
        _state.SetSpeed(requested).SpeedPercent.ShouldBe(expected);

        _state.Practice.SpeedPercent.ShouldBe(expected);
        _state.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void NudgeSpeed_AndTranspose_StepAndClamp()
    {
        _state.NudgeSpeed(-5).SpeedPercent.ShouldBe(95);
        _state.NudgeSpeed(30).SpeedPercent.ShouldBe(120);
        _state.NudgeTranspose(1).TransposeSemitones.ShouldBe(1);
        _state.SetTranspose(-30).TransposeSemitones.ShouldBe(-12);
        _state.NudgeTranspose(-1).TransposeSemitones.ShouldBe(-12);
        _state.SetTranspose(40).TransposeSemitones.ShouldBe(12);
    }

    [Fact]
    public void ResetPractice_RestoresSpeedAndKey_AndKeepsTrainerAndCountIn()
    {
        _state.ApplyMix(TestData.Mix() with { Settings = TestData.Settings() with { Practice = Slowed } });

        var reset = _state.ResetPractice();

        reset.ShouldBe(Slowed with { SpeedPercent = 100, TransposeSemitones = 0 });
        _state.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Trainer_SetAndToggle()
    {
        var trainer = new SpeedTrainerSettingsDto(false, 60, 10, 80, 1);

        _state.SetTrainer(trainer).Trainer.ShouldBe(trainer);
        _state.ToggleTrainer().Trainer.Enabled.ShouldBeTrue();
        _state.ToggleTrainer().Trainer.Enabled.ShouldBeFalse();
        _state.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void CountIn_BarsAndEveryPass_RememberTheLastBarCount()
    {
        _state.SetCountInBars(2).CountIn.Bars.ShouldBe(2);
        _state.SetCountInBars(0).CountIn.Bars.ShouldBe(0);
        _state.LastCountInBars.ShouldBe(2);
        _state.SetCountInBars(7).CountIn.Bars.ShouldBe(2);
        _state.SetCountInEveryPass(true).CountIn.EveryPass.ShouldBeTrue();
        _state.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void ToggleCountIn_AtBeginner_UsesOneBar_ElsewhereTheLastBarCount()
    {
        _state.SetCountInBars(2);
        _state.SetCountInBars(0);

        _state.ToggleCountIn(beginner: true).CountIn.Bars.ShouldBe(1);
        _state.ToggleCountIn(beginner: true).CountIn.Bars.ShouldBe(0);
        _state.ToggleCountIn(beginner: false).CountIn.Bars.ShouldBe(2);
        _state.ToggleCountIn(beginner: false).CountIn.Bars.ShouldBe(0);
    }

    [Fact]
    public void MarkSaved_ClearsTheDirtyFlag_AfterAPracticeChange()
    {
        _state.SetSpeed(80);

        _state.MarkSaved(TestData.Mix());

        _state.IsDirty.ShouldBeFalse();
    }

    // ---- PracticeRules (data-model 3) ----

    [Fact]
    public void TrackBpm_PrefersTheOverride_ThenAReliableGrid()
    {
        var metronome = new MetronomeSettingsDto(true, 0.7, null, 0, 4);

        PracticeRules.TrackBpm(metronome with { BpmOverride = 90 }, TestData.Grid()).ShouldBe(90);
        PracticeRules.TrackBpm(metronome, TestData.Grid()).ShouldBe(113.5);
        PracticeRules.TrackBpm(metronome, TestData.Grid(reliable: false)).ShouldBeNull();
        PracticeRules.TrackBpm(metronome, null).ShouldBeNull();
        PracticeRules.TrackBpm(metronome with { BpmOverride = 120 }, null).ShouldBe(120);
    }

    [Theory]
    [InlineData(120.0, 75, 90)]
    [InlineData(113.5, 100, 114)]
    [InlineData(113.5, 50, 57)]
    public void EffectiveBpm_IsTrackTempoTimesHeardSpeed_Rounded(double bpm, int speed, int expected) =>
        PracticeRules.EffectiveBpm(bpm, speed).ShouldBe(expected);

    [Fact]
    public void EffectiveBpm_WithoutATempo_IsNull() => PracticeRules.EffectiveBpm(null, 80).ShouldBeNull();

    [Theory]
    [InlineData(70, 0, false)]
    [InlineData(69, 0, true)]
    [InlineData(100, 7, false)]
    [InlineData(100, -7, false)]
    [InlineData(100, 8, true)]
    [InlineData(100, -8, true)]
    public void QualityHint_BelowSeventyPercent_OrBeyondSevenSemitones(int speed, int transpose, bool expected) =>
        PracticeRules.NeedsQualityHint(speed, transpose).ShouldBe(expected);

    [Fact]
    public void IsNeutral_IgnoresTheCountIn()
    {
        PracticeRules.IsNeutral(PracticeSettingsDto.Default with { CountIn = new CountInSettingsDto(2, true) }).ShouldBeTrue();
        PracticeRules.IsNeutral(PracticeSettingsDto.Default with { SpeedPercent = 99 }).ShouldBeFalse();
        PracticeRules.IsNeutral(PracticeSettingsDto.Default with { TransposeSemitones = 1 }).ShouldBeFalse();
        PracticeRules.IsNeutral(Slowed with { SpeedPercent = 100, TransposeSemitones = 0 }).ShouldBeFalse();
    }

    [Fact]
    public void HeardSpeed_FollowsTheTrainer_AndTheUnavailableStage()
    {
        var settings = PracticeSettingsDto.Default with { SpeedPercent = 80, TransposeSemitones = 2 };
        var running = PracticeStatus.Initial with { Trainer = new TrainerStatus(TrainerState.Running, 2, 65) };
        var target = PracticeStatus.Initial with { Trainer = new TrainerStatus(TrainerState.Target, 9, 100) };
        var unavailable = PracticeStatus.ForUnavailable("No AudioWorklet");

        PracticeRules.HeardSpeed(settings, PracticeStatus.Initial).ShouldBe(80);
        PracticeRules.HeardSpeed(settings, running).ShouldBe(65);
        PracticeRules.HeardSpeed(settings, target).ShouldBe(100);
        PracticeRules.HeardSpeed(settings, unavailable).ShouldBe(100);
        PracticeRules.HeardTranspose(settings, running).ShouldBe(2);
        PracticeRules.HeardTranspose(settings, unavailable).ShouldBe(0);
        PracticeRules.TrainerRuns(running).ShouldBeTrue();
        PracticeRules.TrainerRuns(PracticeStatus.Initial with { Trainer = new TrainerStatus(TrainerState.Waiting, 0, 60) }).ShouldBeFalse();
    }

    [Fact]
    public void LoopProblem_NeedsAnEnabledLoopOfHalfASecond()
    {
        PracticeRules.LoopProblem(null).ShouldBe(PracticeRules.NeedsLoopMessage);
        PracticeRules.LoopProblem(new LoopRegionDto(10, 20, false)).ShouldBe(PracticeRules.NeedsLoopMessage);
        PracticeRules.LoopProblem(new LoopRegionDto(10, 10.4, true)).ShouldBe(PracticeRules.LoopTooShortMessage);
        PracticeRules.LoopProblem(new LoopRegionDto(10, 10.5, true)).ShouldBeNull();
    }

    // ---- PracticeSender: at most one engine call per 50 ms while dragging (UI-side only) ----

    [Fact]
    public async Task Sender_NeutralSettings_AreNotSent()
    {
        var (sender, sent, _) = Sender();

        await sender.SubmitAsync(PracticeSettingsDto.Default);

        sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sender_SendsTheFirstChangeAtOnce_AndCoalescesTheRest_ToTheLatest()
    {
        var (sender, sent, time) = Sender();

        await sender.SubmitAsync(Speed(90));
        time.Advance(TimeSpan.FromMilliseconds(10));
        await sender.SubmitAsync(Speed(85));
        time.Advance(TimeSpan.FromMilliseconds(10));
        await sender.SubmitAsync(Speed(80));
        sent.Select(s => s.SpeedPercent).ShouldBe([90]);

        time.Advance(TimeSpan.FromMilliseconds(30));

        sent.Select(s => s.SpeedPercent).ShouldBe([90, 80]);
        sender.Sent.SpeedPercent.ShouldBe(80);
    }

    [Fact]
    public async Task Sender_AfterTheInterval_SendsAtOnceAgain()
    {
        var (sender, sent, time) = Sender();

        await sender.SubmitAsync(Speed(90));
        time.Advance(PracticeSender.Interval);
        await sender.SubmitAsync(Speed(95));

        sent.Select(s => s.SpeedPercent).ShouldBe([90, 95]);
    }

    [Fact]
    public async Task Sender_DropsAPendingChange_ThatReturnsToWhatTheEngineHas()
    {
        var (sender, sent, time) = Sender();

        await sender.SubmitAsync(Speed(90));
        await sender.SubmitAsync(Speed(85));
        await sender.SubmitAsync(Speed(90));
        time.Advance(PracticeSender.Interval);

        sent.Select(s => s.SpeedPercent).ShouldBe([90]);
    }

    [Fact]
    public async Task Sender_Disposed_SendsNothingPending()
    {
        var (sender, sent, time) = Sender();

        await sender.SubmitAsync(Speed(90));
        await sender.SubmitAsync(Speed(85));
        sender.Dispose();
        time.Advance(PracticeSender.Interval);

        sent.Select(s => s.SpeedPercent).ShouldBe([90]);
    }

    private static PracticeSettingsDto Speed(int percent) => PracticeSettingsDto.Default with { SpeedPercent = percent };

    private static (PracticeSender Sender, List<PracticeSettingsDto> Sent, FakeTimeProvider Time) Sender()
    {
        var time = new FakeTimeProvider();
        var sent = new List<PracticeSettingsDto>();
        var sender = new PracticeSender(time, settings =>
        {
            sent.Add(settings);
            return Task.CompletedTask;
        });
        return (sender, sent, time);
    }
}
