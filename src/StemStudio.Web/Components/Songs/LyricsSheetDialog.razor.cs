using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Songs;

/// <summary>Creates or updates a lyrics sheet (feature 010, US2); the API normalises the text.</summary>
public sealed partial class LyricsSheetDialog
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The sheet to edit; null for new lyrics.</summary>
    [Parameter]
    public LyricsSheetDto? Sheet { get; set; }

    /// <summary>Cancel or Escape.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Raised with the saved sheet.</summary>
    [Parameter]
    public EventCallback<LyricsSheetDto> OnSaved { get; set; }

    private string Title { get; set; } = string.Empty;

    private string Language { get; set; } = "en";

    private string Text { get; set; } = string.Empty;

    private FieldProblem? Problem { get; set; }

    private bool Busy { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (Sheet is { } sheet)
        {
            (Title, Language, Text) = (sheet.Title, sheet.Language, sheet.Text);
        }
    }

    private Task OnKeyAsync(KeyboardEventArgs args) => CloseOnEscapeAsync(args, OnClose);

    private async Task SaveAsync()
    {
        Problem = Title.Trim().Length is 0 or > ContractLimits.LyricsTitleMaxLength ? new FieldProblem("title", "Enter a title of 1 to 80 characters.") : null;
        if (Problem is null)
        {
            Busy = true;
            await SendAsync(new LyricsInput { Title = Title.Trim(), Language = Language, Text = Text });
            Busy = false;
        }
    }

    private async Task SendAsync(LyricsInput input)
    {
        try
        {
            await OnSaved.InvokeAsync(Sheet is null ? await Api.CreateLyricsSheetAsync(input) : await Api.UpdateLyricsSheetAsync(Sheet.Id, input));
        }
        catch (ApiException ex)
        {
            Problem = FieldProblem.From(ex.Error);
        }
    }
}
