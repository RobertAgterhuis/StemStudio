using System.Globalization;
using Microsoft.AspNetCore.Components;
using StemStudio.Web.Interop;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Mixer;

/// <summary>
/// Mixer keyboard commands (design-spec.md 6, FR-019) and the mixer's command-palette actions. Every command calls
/// the same method as the matching button; channel keys select a standard strip (<c>data-selected</c>) for M/S.
/// </summary>
public sealed partial class Mixer
{
    private readonly List<IDisposable> _actions = [];
    private IAsyncDisposable? _mixerKeys;

    [Inject]
    private IWebInterop Web { get; set; } = default!;

    [Inject]
    private PageCommands Commands { get; set; } = default!;

    /// <summary>Engine channel id of the selected standard strip, or <see langword="null"/>.</summary>
    private string? SelectedStem { get; set; }

    private async Task RegisterKeysAsync()
    {
        _mixerKeys = await Web.RegisterShortcutsAsync(ShortcutScope.Mixer, id => InvokeAsync(() => OnMixerCommandAsync(id)));
        _actions.Add(Commands.Register("mixer-save", "Save mix", () => ActionAsync(SaveAsync)));
        _actions.Add(Commands.Register("mixer-save-as", "Save mix as", () => ActionAsync(() => SaveCommandAsync("mixer.save-as"))));
        _actions.Add(Commands.Register("mixer-play", "Play or pause", () => ActionAsync(TogglePlaybackAsync)));
        RegisterPracticeActions();
    }

    private Task ActionAsync(Func<Task> action) => InvokeAsync(async () =>
    {
        await action();
        StateHasChanged();
    });

    private async Task ReleaseKeysAsync()
    {
        _actions.ForEach(a => a.Dispose());
        _actions.Clear();
        await (_mixerKeys?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    private async Task OnMixerCommandAsync(string commandId)
    {
        var parts = commandId.Split(':', 2);
        var argument = parts.Length > 1 ? double.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
        await (parts[0] switch
        {
            "mixer.stop" => StopAsync(),
            "mixer.seek" => SeekAsync(Math.Clamp(Position + argument, 0, State!.Duration)),
            "mixer.loop.a" => SetLoopStartAsync(Position),
            "mixer.loop.b" => SetLoopEndAsync(Position),
            "mixer.loop.toggle" => ToggleLoopAsync(),
            _ => ChannelCommandAsync(parts[0], (int)argument),
        });
        StateHasChanged();
    }

    private Task ChannelCommandAsync(string command, int channel) => command switch
    {
        "mixer.metronome.toggle" => SetMetronomeAsync(State!.Metronome with { Enabled = !State.Metronome.Enabled }),
        "mixer.channel.select" => Select(channel),
        "mixer.channel.mute" => SelectedStem is { } stem ? ToggleMuteAsync(stem) : Task.CompletedTask,
        "mixer.channel.solo" => SelectedStem is { } soloed ? ToggleSoloAsync(soloed) : Task.CompletedTask,
        _ => PracticeCommandAsync(command, channel),
    };

    private Task SaveCommandAsync(string command)
    {
        switch (command)
        {
            case "mixer.save":
                return SaveAsync();
            case "mixer.save-as":
                OpenSaveAs();
                break;
        }

        return Task.CompletedTask;
    }

    private Task Select(int channel)
    {
        SelectedStem = State!.Channels.ElementAtOrDefault(channel - 1)?.Stem ?? SelectedStem;
        return Task.CompletedTask;
    }
}
