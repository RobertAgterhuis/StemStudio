using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>The one-time Claude notice of the lyric assistant (FR-026): nothing is sent before "I agree".</summary>
public sealed partial class ClaudeNoticeDialog
{
    private const string DefaultText = "Your lyrics text is sent to Anthropic for this check; paid per use.";
    private string? _text;
    private bool _busy;

    /// <summary>The editor link.</summary>
    [Parameter, EditorRequired]
    public EditorHost Host { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        _text = (await Host.Session.LoadSettingsAsync())?.ClaudeNoticeText;
    }

    private Task OnKeyAsync(KeyboardEventArgs args) => CloseOnEscapeAsync(args, EventCallback.Factory.Create(this, CancelAsync));

    private Task CancelAsync()
    {
        Host.Session.CancelNotice();
        Host.Session.NotifyChanged();
        return Task.CompletedTask;
    }

    private async Task AcknowledgeAsync()
    {
        _busy = true;
        await Host.Session.AcknowledgeNoticeAsync();
        _busy = false;
        Host.Session.NotifyChanged();
    }
}
