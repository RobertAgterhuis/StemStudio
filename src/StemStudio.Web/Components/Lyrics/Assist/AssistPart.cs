using Microsoft.AspNetCore.Components;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>
/// Base of the pane's parts: the editor link and <see cref="RunAsync"/>, which runs a session action and then tells every
/// listener (the pane and the page) that the state changed, so the text, the highlights and the list stay in step.
/// </summary>
public abstract class AssistPart : ComponentBase
{
    /// <summary>The editor link (session and selection).</summary>
    [Parameter, EditorRequired]
    public EditorHost Host { get; set; } = default!;

    /// <summary>The session.</summary>
    protected LyricAssistSession Session => Host.Session;

    /// <summary>True while an action of this part runs (its buttons are disabled).</summary>
    protected bool Busy { get; private set; }

    /// <summary>Runs an action and announces the change.</summary>
    /// <param name="action">The action.</param>
    /// <returns>A task that completes when done.</returns>
    protected async Task RunAsync(Func<LyricAssistSession, Task> action)
    {
        Busy = true;
        try
        {
            await action(Session);
        }
        finally
        {
            Busy = false;
            Session.NotifyChanged();
        }
    }
}
