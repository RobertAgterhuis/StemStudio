using System.Globalization;
using StemStudio.Contracts.Library;

namespace StemStudio.Web.Library;

/// <summary>
/// The polite live-region text of the search (feature 022, FR-027, data-model 7): the result count, announced once 500 ms
/// after the last change so a screen reader is not interrupted at every key press.
/// </summary>
/// <param name="time">The clock of the pause.</param>
/// <param name="dispatch">Runs the update on the renderer's synchronisation context.</param>
public sealed class LibraryAnnouncer(TimeProvider time, Func<Func<Task>, Task> dispatch) : IDisposable
{
    /// <summary>The pause after the last change.</summary>
    public static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(500);

    private ITimer? _timer;
    private string? _pending;

    /// <summary>Raised when <see cref="Message"/> changed.</summary>
    public event Action? Changed;

    /// <summary>The text of the live region (empty until the first announcement).</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>The count text of a result.</summary>
    /// <param name="result">The result.</param>
    /// <returns>"No results", "1 result" or "12 results".</returns>
    public static string CountText(LibrarySearchResultDto result)
    {
        var count = result.Totals.Artists + result.Totals.Albums + result.Totals.Songs + result.Totals.Items;
        return count switch
        {
            0 => "No results",
            1 => "1 result",
            _ => $"{count.ToString("N0", CultureInfo.InvariantCulture)} results",
        };
    }

    /// <summary>Schedules the announcement of a result (or of nothing, when the search was cleared).</summary>
    /// <param name="result">The newest result, or null.</param>
    public void Schedule(LibrarySearchResultDto? result)
    {
        _timer?.Dispose();
        _pending = result is null ? string.Empty : CountText(result);
        if (_pending.Length == 0)
        {
            Publish();
            return;
        }

        _timer = time.CreateTimer(_ => _ = dispatch(PublishAsync), null, Pause, Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc />
    public void Dispose() => _timer?.Dispose();

    private Task PublishAsync()
    {
        Publish();
        return Task.CompletedTask;
    }

    private void Publish()
    {
        if (_pending is { } text && text != Message)
        {
            Message = text;
            Changed?.Invoke();
        }

        _pending = null;
    }
}
