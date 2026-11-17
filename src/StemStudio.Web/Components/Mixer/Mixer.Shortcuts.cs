using Microsoft.AspNetCore.Components;
using StemStudio.Web.Interop;
using StemStudio.Web.Mixer;
using StemStudio.Web.Practice;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Mixer;

/// <summary>
/// Mixer keyboard commands (design-spec.md 6, 014 FR-019/FR-020) and the mixer's command-palette actions. The key map is
/// data (web-interop 1.1.0, <see cref="ShortcutKeys"/>); every command id goes through <see cref="MixerCommandRegistry"/>
/// to the same method as its control. Channel keys select a standard strip (<c>data-selected</c>) for M/S.
/// Other features register their commands in partial methods (015: <c>RegisterRecordingCommands</c>).
/// </summary>
public sealed partial class Mixer
{
    private readonly List<IDisposable> _actions = [];
    private readonly MixerCommandRegistry _commands = new();
    private IAsyncDisposable? _mixerKeys;

    [Inject]
    private ShortcutKeys Keys { get; set; } = default!;

    [Inject]
    private PageCommands Commands { get; set; } = default!;

    /// <summary>Engine channel id of the selected standard strip, or <see langword="null"/>.</summary>
    private string? SelectedStem { get; set; }

    /// <summary>Hook of feature 015 (<c>Mixer.Recording.cs</c>): registers <c>mixer.record.toggle</c> and <c>mixer.compare.cycle</c>.</summary>
    /// <param name="commands">The mixer's command registry.</param>
    partial void RegisterRecordingCommands(MixerCommandRegistry commands);

    private async Task RegisterKeysAsync()
    {
        RegisterCommands();
        _mixerKeys = await Keys.RegisterAsync(ShortcutScope.Mixer, id => InvokeAsync(() => OnMixerCommandAsync(id)));
        _actions.Add(Commands.Register("mixer-save", "Save mix", () => ActionAsync(SaveAsync)));
        _actions.Add(Commands.Register("mixer-save-as", "Save mix as", () => ActionAsync(() => SaveCommandAsync("mixer.save-as"))));
        _actions.Add(Commands.Register("mixer-play", "Play or pause", () => ActionAsync(TogglePlaybackAsync)));
        RegisterPracticeActions();
    }

    private void RegisterCommands()
    {
        _commands.Add("mixer.play-pause", TogglePlaybackAsync);
        _commands.Add("mixer.stop", StopAsync);
        _commands.Add("mixer.seek", seconds => SeekAsync(Math.Clamp(Position + seconds, 0, State!.Duration)));
        _commands.Add("mixer.loop.a", () => SetLoopStartAsync(Position));
        _commands.Add("mixer.loop.b", () => SetLoopEndAsync(Position));
        _commands.Add("mixer.loop.toggle", ToggleLoopAsync);
        _commands.Add("mixer.metronome.toggle", () => SetMetronomeAsync(State!.Metronome with { Enabled = !State.Metronome.Enabled }));
        _commands.Add("mixer.channel.select", channel => Select((int)channel));
        _commands.Add("mixer.channel.mute", () => SelectedStem is { } stem ? ToggleMuteAsync(stem) : Task.CompletedTask);
        _commands.Add("mixer.channel.solo", () => SelectedStem is { } stem ? ToggleSoloAsync(stem) : Task.CompletedTask);
        _commands.Add("mixer.save", SaveAsync);
        _commands.Add("mixer.save-as", () => SaveCommandAsync("mixer.save-as"));
        RegisterPracticeCommands(_commands);
        RegisterSectionCommands(_commands);
        RegisterRecordingCommands(_commands);
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
        if (State is not null && await _commands.DispatchAsync(commandId))
        {
            StateHasChanged();
        }
    }

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
