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

    private Task SubmitPracticeAsync(PracticeSettingsDto settings) => EngineReady ? PracticeSender.SubmitAsync(settings) : Task.CompletedTask;

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
