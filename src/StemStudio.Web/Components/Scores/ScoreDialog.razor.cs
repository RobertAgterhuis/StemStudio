using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Api;
using StemStudio.Web.Interop;
using StemStudio.Web.Scores;

namespace StemStudio.Web.Components.Scores;

/// <summary>Loads the score options of a <see cref="ScoreTarget"/>, submits the choices and confirms the new item (feature 008).</summary>
public sealed partial class ScoreDialog : IAsyncDisposable
{
    private ElementReference _title;
    private IAsyncDisposable? _returnFocus;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IWebInterop Web { get; set; } = default!;

    /// <summary>What is notated.</summary>
    [Parameter, EditorRequired]
    public ScoreTarget Target { get; set; } = default!;

    /// <summary>Cancel, Close or Escape.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Raised with the new item once it is created (the library reloads its list).</summary>
    [Parameter]
    public EventCallback<AssetDto> OnSaved { get; set; }

    private ScoreForm? Form { get; set; }

    private string? Error { get; set; }

    private bool Busy { get; set; }

    private AssetDto? Saved { get; set; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_returnFocus is not null)
        {
            await _returnFocus.DisposeAsync();
        }
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        _returnFocus = await Web.CaptureFocusAsync();
        try
        {
            Form = new ScoreForm(await Api.GetScoreOptionsAsync(Target.Source), Target);
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await _title.FocusAsync();
        }
    }

    private Task OnKeyAsync(KeyboardEventArgs args) => args.Key == "Escape" ? OnClose.InvokeAsync() : Task.CompletedTask;

    private async Task SubmitAsync()
    {
        Error = Form!.Validate();
        if (Error is null)
        {
            Busy = true;
            await SaveAsync();
            Busy = false;
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            Saved = await Api.CreateScoreAsync(Form!.Request());
            await OnSaved.InvokeAsync(Saved);
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }
}
