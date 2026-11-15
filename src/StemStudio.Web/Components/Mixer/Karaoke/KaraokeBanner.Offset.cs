using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Web.Api;
using StemStudio.Web.Lyrics;

namespace StemStudio.Web.Components.Mixer.Karaoke;

/// <summary>The karaoke offset (FR-007): applied to the engine at once, stored debounced.</summary>
public sealed partial class KaraokeBanner
{
    private CancellationTokenSource? _save;
    private int? _pendingOffset;
    private bool _arrowPressed;

    /// <summary>How long the offset must stay unchanged before it is stored.</summary>
    public static TimeSpan SaveDelay { get; } = TimeSpan.FromMilliseconds(500);

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    private int Offset { get; set; }

    private string? Error { get; set; }

    private void OnOffsetKey(KeyboardEventArgs args) => _arrowPressed = OffsetInput.IsArrow(args.Key);

    private Task OnOffsetInputAsync(ChangeEventArgs args)
    {
        var arrow = _arrowPressed;
        _arrowPressed = false;
        return OffsetInput.Parse(args.Value) is { } next ? ChangeOffsetAsync(arrow ? OffsetInput.AfterKey(Offset, next) : next) : Task.CompletedTask;
    }

    private Task ResetOffsetAsync() => ChangeOffsetAsync(0);

    private async Task ChangeOffsetAsync(int offsetMs)
    {
        Offset = offsetMs;
        await (_attached is null ? Task.CompletedTask : Engine.SetKaraokeOffsetAsync(offsetMs));
        ScheduleSave(offsetMs);
    }

    private void ScheduleSave(int offsetMs)
    {
        _save?.Cancel();
        _save?.Dispose();
        _save = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        _pendingOffset = offsetMs;
        _ = SaveLaterAsync(offsetMs, _save.Token);
    }

    private async Task SaveLaterAsync(int offsetMs, CancellationToken ct)
    {
        try
        {
            await Task.Delay(SaveDelay, Time, ct);
            _pendingOffset = null;
            await SaveAsync(offsetMs, ct);
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            // A newer offset replaced this one, or the mixer closed (the pending value is flushed on dispose).
        }
    }

    private async Task SaveAsync(int offsetMs, CancellationToken ct)
    {
        try
        {
            await Api.SetLyricsOffsetAsync(TrackId, offsetMs, ct);
            Error = null;
        }
        catch (ApiException ex)
        {
            Error = $"The offset could not be saved: {ex.Error.UserMessage}";
        }
    }

    /// <summary>Stores an offset that was still waiting for its delay when the mixer closed.</summary>
    private void FlushPendingSave()
    {
        if (_pendingOffset is { } offsetMs)
        {
            _ = SaveAsync(offsetMs, CancellationToken.None);
        }
    }
}
