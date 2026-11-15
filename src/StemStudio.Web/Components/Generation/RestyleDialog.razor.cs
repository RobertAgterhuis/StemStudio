using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Generation;

namespace StemStudio.Web.Components.Generation;

/// <summary>Loads the style library, keeps the choices in a <see cref="RestyleForm"/> and starts the restyle (feature 009).</summary>
public sealed partial class RestyleDialog
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The completed track to restyle.</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    /// <summary>Cancel or Escape.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Raised with the queued generation; the parent closes the dialog and shows it in its list.</summary>
    [Parameter]
    public EventCallback<GenerationDto> OnStarted { get; set; }

    private RestyleForm? Form { get; set; }

    private string? Error { get; set; }

    private bool Busy { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        try
        {
            Form = new RestyleForm(Track, await Api.ListStylesAsync());
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }

    private Task OnKeyAsync(KeyboardEventArgs args) => args.Key == "Escape" ? OnClose.InvokeAsync() : Task.CompletedTask;

    private void OnStyleChanged(ChangeEventArgs args) => Form!.SelectStyle(Guid.Parse((string)args.Value!));

    private async Task StartAsync()
    {
        Error = Form!.Validate();
        if (Error is null)
        {
            Busy = true;
            await SendAsync();
            Busy = false;
        }
    }

    private async Task SendAsync()
    {
        try
        {
            await OnStarted.InvokeAsync(await Api.CreateRestyleAsync(Track.Id, Form!.Request()));
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }
}
