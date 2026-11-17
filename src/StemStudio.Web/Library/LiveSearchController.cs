using StemStudio.Contracts;
using StemStudio.Contracts.Library;
using StemStudio.Web.Api;

namespace StemStudio.Web.Library;

/// <summary>What the live search needs from its page: the API call, navigation, focus and the renderer's dispatcher.</summary>
/// <param name="Search">Runs one search (the token is cancelled when a newer text replaces it).</param>
/// <param name="Navigate">Changes the URL: <c>(url, replace)</c>; replace while typing, push on Enter.</param>
/// <param name="FocusBox">Moves focus to the search box (after clearing).</param>
/// <param name="Dispatch">Runs work on the renderer's synchronisation context (timer callbacks arrive on the pool).</param>
public sealed record LiveSearchPorts(
    Func<LibrarySearchRequest, CancellationToken, Task<LibrarySearchResultDto>> Search,
    Action<string, bool> Navigate,
    Func<ValueTask> FocusBox,
    Func<Func<Task>, Task> Dispatch);

/// <summary>
/// The live library search (feature 022, FR-022, FR-026, research R-07): searches from the first character 200 ms after
/// the last key press (Enter at once), numbers every request and drops answers older than the newest shown, cancels
/// the request in flight, keeps <c>?q=</c> in the URL (replaced while typing, pushed on Enter) and restores the folder
/// that was open when the search is cleared.
/// </summary>
public sealed class LiveSearchController : IDisposable
{
    /// <summary>The pause after the last key press.</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(200);

    private readonly LiveSearchPorts _ports;
    private readonly TimeProvider _time;
    private LibraryLocation _origin = LibraryLocation.Root;
    private ITimer? _timer;
    private CancellationTokenSource? _inFlight;
    private long _sequence;

    /// <summary>Creates the controller.</summary>
    /// <param name="ports">The page's ports.</param>
    /// <param name="time">The clock of the debounce.</param>
    public LiveSearchController(LiveSearchPorts ports, TimeProvider time) => (_ports, _time) = (ports, time);

    /// <summary>Raised after the text, the results or the error changed.</summary>
    public event Action? Changed;

    /// <summary>The text in the box.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>The results of the newest answered text, or null while there is none (blank text).</summary>
    public LibrarySearchResultDto? Result { get; private set; }

    /// <summary>Why the last search failed (the previous results stay), or null.</summary>
    public string? Error { get; private set; }

    /// <summary>True while a search for the current text is pending or running.</summary>
    public bool Busy { get; private set; }

    /// <summary>The folder the search is limited to ("Only in this folder", Advanced), or null.</summary>
    public string? Within { get; private set; }

    /// <summary>Hits per group asked for: null = 20, the maximum after "Show all".</summary>
    public int? Limit { get; private set; }

    /// <summary>True while the text is not blank (results replace the folder view).</summary>
    public bool IsActive => !string.IsNullOrWhiteSpace(Text);

    /// <summary>Takes over a location (page load, Back, a link): the folder to return to and the text of <c>?q=</c>.</summary>
    /// <param name="location">The current location.</param>
    /// <returns>A task that completes when the search of a restored text answered.</returns>
    public Task StartAsync(LibraryLocation location)
    {
        _origin = location.WithQuery(null);
        if (Within is not null && Within != location.FolderId)
        {
            Within = null;
        }

        var text = location.Query ?? string.Empty;
        if (text == Text)
        {
            return Task.CompletedTask;
        }

        Text = text;
        return IsActive ? RunAsync() : ResetAsync();
    }

    /// <summary>A key press changed the text: debounced search, URL replaced.</summary>
    /// <param name="text">The new text (cut at 100 characters).</param>
    public void Input(string? text)
    {
        Text = _origin.WithQuery(text).Query ?? (text ?? string.Empty);
        Limit = null;
        _ports.Navigate(_origin.WithQuery(Text).ToUrl(), true);
        StopTimer();
        if (!IsActive)
        {
            _ = ResetAsync();
            return;
        }

        Busy = true;
        _timer = _time.CreateTimer(_ => _ = _ports.Dispatch(RunAsync), null, Debounce, Timeout.InfiniteTimeSpan);
        Changed?.Invoke();
    }

    /// <summary>Enter: searches at once and pushes the URL so Back returns to it.</summary>
    /// <returns>A task that completes when the search answered.</returns>
    public Task SubmitAsync()
    {
        StopTimer();
        if (!IsActive)
        {
            return Task.CompletedTask;
        }

        _ports.Navigate(_origin.WithQuery(Text).ToUrl(), false);
        return RunAsync();
    }

    /// <summary>Clear button or Escape: back to the folder that was open, focus in the box.</summary>
    /// <returns>A task that completes when focus moved.</returns>
    public async Task ClearAsync()
    {
        StopTimer();
        var wasActive = IsActive;
        Text = string.Empty;
        await ResetAsync();
        if (wasActive)
        {
            _ports.Navigate(_origin.ToUrl(), false);
        }

        await _ports.FocusBox();
    }

    /// <summary>Limits the search to a folder or lifts the limit, and searches again.</summary>
    /// <param name="folderId">The folder, or null for the whole library.</param>
    /// <returns>A task that completes when the search answered.</returns>
    public Task ScopeAsync(string? folderId)
    {
        Within = folderId;
        return RefreshAsync();
    }

    /// <summary>"Show all": asks for the largest groups and searches again.</summary>
    /// <returns>A task that completes when the search answered.</returns>
    public Task ShowAllAsync()
    {
        Limit = ContractLimits.LibrarySearchGroupMax;
        return RefreshAsync();
    }

    /// <summary>Searches the current text again (an item in the results changed).</summary>
    /// <returns>A task that completes when the search answered.</returns>
    public Task RefreshAsync() => IsActive ? RunAsync() : Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        StopTimer();
        Cancel();
    }

    private Task ResetAsync()
    {
        Cancel();
        _sequence++;
        (Result, Error, Busy) = (null, null, false);
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        StopTimer();
        Cancel();
        var sequence = ++_sequence;
        Busy = true;
        var (result, error, cancelled) = await TrySearchAsync((_inFlight = new CancellationTokenSource()).Token);
        if (cancelled || sequence != _sequence)
        {
            return;
        }

        (Result, Error, Busy) = (result ?? Result, error, false);
        Changed?.Invoke();
    }

    private async Task<(LibrarySearchResultDto? Result, string? Error, bool Cancelled)> TrySearchAsync(CancellationToken token)
    {
        try
        {
            return (await _ports.Search(new LibrarySearchRequest(Text.Trim(), Within, Limit), token), null, false);
        }
        catch (OperationCanceledException)
        {
            return (null, null, true);
        }
        catch (ApiException ex)
        {
            return (null, ex.Error.UserMessage, false);
        }
    }

    private void Cancel()
    {
        _inFlight?.Cancel();
        _inFlight?.Dispose();
        _inFlight = null;
    }

    private void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
