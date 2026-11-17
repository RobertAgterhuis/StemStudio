using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Audio;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Components.Mixer;

/// <summary>
/// Practice tempo of the mixer page (012): updates <c>MixerState.Practice</c>, sends it through the 50 ms
/// <see cref="PracticeSender"/> and shows the engine's <see cref="PracticeStatus"/> (marshalled to the renderer).
/// </summary>
public sealed partial class Mixer
{
    private PracticeSender? _practiceSender;

    [Inject]
    private TimeProvider Clock { get; set; } = default!;

    /// <summary>The engine's last practice snapshot.</summary>
    private PracticeStatus PracticeLive { get; set; } = PracticeStatus.Initial;

    private double? TrackBpm => PracticeRules.TrackBpm(State!.Metronome, _track?.BeatGrid);

    private bool StretchUnavailable => PracticeLive.Stretch == StretchState.Unavailable;

    private PracticeSender PracticeSender => _practiceSender ??= new PracticeSender(Clock, s => InvokeAsync(() => SendPracticeAsync(s)));

    private Task SetSpeedAsync(int percent) => SubmitPracticeAsync(State!.SetSpeed(percent));

    private Task SetTransposeAsync(int semitones) => SubmitPracticeAsync(State!.SetTranspose(semitones));

    private Task ResetPracticeAsync() => SubmitPracticeAsync(State!.ResetPractice());

    private Task SetTrainerAsync(SpeedTrainerSettingsDto trainer) => SubmitPracticeAsync(State!.SetTrainer(trainer));

    private Task ToggleTrainerAsync() => SubmitPracticeAsync(State!.ToggleTrainer());

    // The trainer key turns the trainer on only where its switch could (Advanced+, usable loop, stage available).
    private Task TrainerCommandAsync() =>
        IsBeginner || (!State!.Practice.Trainer.Enabled && (StretchUnavailable || PracticeRules.LoopProblem(State.Loop) is not null))
            ? Task.CompletedTask
            : ToggleTrainerAsync();

    private Task ToggleCountInAsync() => SubmitPracticeAsync(State!.ToggleCountIn(IsBeginner));

    private Task SetCountInBarsAsync(int bars) => SubmitPracticeAsync(State!.SetCountInBars(bars));

    private Task SetCountInEveryPassAsync(bool everyPass) => SubmitPracticeAsync(State!.SetCountInEveryPass(everyPass));

    // The count-in key turns it on only where its switch could (a tempo is known); it always turns it off.
    private Task CountInCommandAsync() =>
        State!.Practice.CountIn.Bars == 0 && TrackBpm is null ? Task.CompletedTask : ToggleCountInAsync();

    // The state changes at once; the engine call is not awaited by the caller: a RadzenButton ignores clicks while its
    // Click handler runs, so awaiting the engine round trip dropped rapid -5/+5 % and -1/+1 clicks (012 integration).
    // SendPracticeAsync reports engine errors itself, like the sender's deferred sends.
    private Task SubmitPracticeAsync(PracticeSettingsDto settings)
    {
        if (EngineReady)
        {
            _ = PracticeSender.SubmitAsync(settings);
        }

        return Task.CompletedTask;
    }

    private async Task SendPracticeAsync(PracticeSettingsDto settings)
    {
        try
        {
            await Engine.SetPracticeAsync(settings);
        }
        catch (AudioEngineException ex)
        {
            EngineError = ex.Message;
        }

        StateHasChanged();
    }

    // Keyboard commands (ui-test-ids 1.11.0 "Shortcut commands"): the same methods as the buttons, nothing while the stage
    // is unavailable; the speed keys also do nothing while the trainer controls the speed.
    private Task PracticeCommandAsync(string command, int argument) => command switch
    {
        "mixer.speed" => SpeedLocked ? Task.CompletedTask : SetSpeedAsync(State!.Practice.SpeedPercent + argument),
        "mixer.transpose" => StretchUnavailable ? Task.CompletedTask : SetTransposeAsync(State!.Practice.TransposeSemitones + argument),
        "mixer.practice.reset" => StretchUnavailable ? Task.CompletedTask : ResetPracticeAsync(),
        "mixer.trainer.toggle" => TrainerCommandAsync(),
        "mixer.countin.toggle" => CountInCommandAsync(),
        _ => SaveCommandAsync(command),
    };

    private bool SpeedLocked => StretchUnavailable || PracticeRules.TrainerRuns(PracticeLive);

    private void RegisterPracticeActions()
    {
        _actions.Add(Commands.Register("mixer-practice-reset", "Original speed and key", () => ActionAsync(() => PracticeCommandAsync("mixer.practice.reset", 0))));
        _actions.Add(Commands.Register("mixer-speed-up", "Speed +5 %", () => ActionAsync(() => PracticeCommandAsync("mixer.speed", 5))));
        _actions.Add(Commands.Register("mixer-speed-down", "Speed -5 %", () => ActionAsync(() => PracticeCommandAsync("mixer.speed", -5))));
    }

    private void OnPracticeChanged(object? sender, PracticeStatus status) =>
        _ = InvokeAsync(() =>
        {
            PracticeLive = status;
            StateHasChanged();
        });

    private void ReleasePractice()
    {
        Engine.PracticeChanged -= OnPracticeChanged;
        _practiceSender?.Dispose();
    }
}
