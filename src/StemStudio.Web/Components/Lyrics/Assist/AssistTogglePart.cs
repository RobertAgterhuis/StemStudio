namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>A pane part that opens on demand and loads its list when it opens (marks, words, revisions).</summary>
public abstract class AssistTogglePart : AssistPart
{
    /// <summary>True while the part is open.</summary>
    protected bool IsOpen { get; private set; }

    /// <summary>Opens or closes the part; opening loads the list.</summary>
    /// <returns>A task that completes when loaded.</returns>
    protected async Task ToggleAsync()
    {
        IsOpen = !IsOpen;
        if (IsOpen)
        {
            await RunAsync(LoadAsync);
        }
    }

    /// <summary>Loads the list of the part.</summary>
    /// <param name="session">The session.</param>
    /// <returns>A task that completes when loaded.</returns>
    protected abstract Task LoadAsync(LyricAssistSession session);
}
