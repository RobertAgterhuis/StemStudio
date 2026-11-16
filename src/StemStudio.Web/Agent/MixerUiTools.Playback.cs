using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Agent;

/// <summary>Transport, A-B loop (seconds or bars from the beat grid), metronome and karaoke tools of the open mixer.</summary>
public sealed partial class MixerUiTools
{
    private async Task<object> TransportAsync(UiToolArguments args)
    {
        var action = args.RequiredText("action");
        (Func<Task> Run, bool Playing, double? Position) step = action switch
        {
            "play" => (_controls.PlayAsync, true, null),
            "pause" => (_controls.PauseAsync, false, null),
            "stop" => (_controls.StopAsync, false, 0),
            "seek" => (SeekTo(args.RequiredNumber("positionSeconds")), _controls.Playing, args.RequiredNumber("positionSeconds")),
            _ => throw new UiToolException($"Unknown transport action '{action}'."),
        };
        await step.Run();
        await _controls.RefreshAsync();
        return new { playing = step.Playing, position = step.Position ?? _controls.Position };
    }

    private Func<Task> SeekTo(double seconds) => () => _controls.SeekAsync(seconds);

    private async Task<object> SetLoopAsync(UiToolArguments args)
    {
        var enabled = args.RequiredBool("enabled");
        var (start, end) = LoopRange(args);
        var previous = State.Loop;
        await (start is { } a ? _controls.SetLoopStartAsync(a) : Task.CompletedTask);
        await (end is { } b ? _controls.SetLoopEndAsync(b) : Task.CompletedTask);
        if (State.LoopEnabled != enabled && (start is null || State.LoopError is null))
        {
            await _controls.ToggleLoopAsync();
        }

        await _controls.RefreshAsync();
        return State.LoopError is { } error ? throw new UiToolException(error) : new { previous, current = State.Loop };
    }

    private (double? Start, double? End) LoopRange(UiToolArguments args)
    {
        var (startBar, endBar) = (args.Whole("startBar"), args.Whole("endBar"));
        if (startBar is null && endBar is null)
        {
            return (args.Number("startSeconds"), args.Number("endSeconds"));
        }

        var grid = _controls.BeatGrid ?? throw new UiToolException("This track has no beat grid; give the loop in seconds.");
        return (startBar is { } s ? BarStart(grid, s) : null, endBar is { } e ? BarEnd(grid, e) : null);
    }

    private static double BarStart(BeatGridDto grid, int bar) => grid.Downbeats[CheckBar(grid, bar) - 1];

    private double BarEnd(BeatGridDto grid, int bar) => CheckBar(grid, bar) < grid.Downbeats.Count ? grid.Downbeats[bar] : State.Duration;

    private static int CheckBar(BeatGridDto grid, int bar) => bar >= 1 && bar <= grid.Downbeats.Count
        ? bar
        : throw new UiToolException($"Bar {bar} is outside the beat grid of this track ({grid.Downbeats.Count} bars).");

    private async Task<object> SetMetronomeAsync(UiToolArguments args)
    {
        var previous = State.Metronome;
        await _controls.SetMetronomeAsync(previous with
        {
            Enabled = args.Bool("enabled") ?? previous.Enabled,
            Volume = args.Number("volume") ?? previous.Volume,
            BeatsPerBar = args.Whole("beatsPerBar") ?? previous.BeatsPerBar,
            OffsetMs = args.Whole("offsetMs") ?? previous.OffsetMs,
        });
        await _controls.RefreshAsync();
        return new { previous, current = State.Metronome };
    }

    private async Task<object> ShowKaraokeAsync(UiToolArguments args)
    {
        var enabled = args.RequiredBool("enabled");
        if (enabled && !_controls.HasSyncedLyrics)
        {
            throw new UiToolException("This track has no synced lyrics, so the karaoke banner cannot be shown.");
        }

        var previous = _controls.KaraokeVisible;
        await (previous == enabled ? Task.CompletedTask : _controls.SetKaraokeVisibleAsync(enabled));
        await _controls.RefreshAsync();
        return new { previous, current = _controls.KaraokeVisible };
    }
}
