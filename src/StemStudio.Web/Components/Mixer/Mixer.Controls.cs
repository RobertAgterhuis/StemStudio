using StemStudio.Contracts.Mixes;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Components.Mixer;

/// <summary>Control handlers of the mixer page: update <c>MixerState</c>, then call the engine.</summary>
public sealed partial class Mixer
{
    private Task SetVolumeAsync(string stem, double volume) => SendChannelAsync(State!.SetVolume(stem, volume));

    private Task SetPanAsync(string stem, double pan) => SendChannelAsync(State!.SetPan(stem, pan));

    private Task ToggleMuteAsync(string stem) => SendChannelAsync(State!.ToggleMute(stem));

    private Task ToggleSoloAsync(string stem) => SendChannelAsync(State!.ToggleSolo(stem));

    private Task SetMasterAsync(double volume)
    {
        var gain = State!.SetMaster(volume);
        return EngineAsync(e => e.SetMasterAsync(gain));
    }

    private Task SetMetronomeAsync(MetronomeSettingsDto settings) => SendMetronomeAsync(State!.SetMetronome(settings));

    private Task ResetMetronomeAsync() => SendMetronomeAsync(State!.ResetMetronome());

    private Task SendMetronomeAsync(MetronomeSettingsDto settings) => EngineAsync(e => e.SetMetronomeAsync(settings));

    private Task PlayAsync() => EngineAsync(e => e.PlayAsync());

    private Task PauseAsync() => EngineAsync(e => e.PauseAsync());

    private Task StopAsync() => EngineAsync(e => e.StopAsync());

    private Task SeekAsync(double seconds) => EngineAsync(e => e.SeekAsync(seconds));

    private Task SetLoopStartAsync(double seconds) => SendLoopAsync(State!.SetLoopStart(seconds));

    private Task SetLoopEndAsync(double seconds) => SendLoopAsync(State!.SetLoopEnd(seconds));

    private Task ToggleLoopAsync() => SendLoopAsync(State!.ToggleLoop());

    private Task SendChannelAsync(ChannelSettingsDto channel) => EngineAsync(e => e.SetChannelAsync(channel));

    private async Task SendLoopAsync(LoopRegionDto? region)
    {
        if (region is null || !EngineReady)
        {
            return;
        }

        try
        {
            await Engine.SetLoopAsync(region);
        }
        catch (AudioEngineException ex)
        {
            State!.RejectLoop(ex.Message);
        }
    }

    private async Task EngineAsync(Func<IAudioEngine, Task> call)
    {
        if (!EngineReady)
        {
            return;
        }

        try
        {
            await call(Engine);
            EngineError = null;
        }
        catch (AudioEngineException ex)
        {
            EngineError = ex.Message;
        }
    }
}
