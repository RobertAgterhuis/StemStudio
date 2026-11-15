using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Api;
using StemStudio.Web.Scores;

namespace StemStudio.Web.Components.Scores;

/// <summary>Loads the score options of a <see cref="ScoreTarget"/>, submits the choices and confirms the new item (feature 008).</summary>
public sealed partial class ScoreDialog
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

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
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        try
        {
            Form = new ScoreForm(await Api.GetScoreOptionsAsync(Target.Source), Target);
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
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
