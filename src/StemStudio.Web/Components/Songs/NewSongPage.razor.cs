using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Generation;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Generation;
using StemStudio.Web.Generation;
using StemStudio.Web.Songs;

namespace StemStudio.Web.Components.Songs;

/// <summary>Loads the styles and lyrics sheets, keeps the choices in a <see cref="SongForm"/> and starts the song (feature 010).</summary>
public sealed partial class NewSongPage
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private GenerationStatusSource StatusSource { get; set; } = default!;

    /// <summary>A saved sheet to start from (<c>?lyricsId=</c>, from the lyrics list).</summary>
    [SupplyParameterFromQuery(Name = "lyricsId")]
    public Guid? LyricsId { get; set; }

    private SongForm? Form { get; set; }

    private GenerationStatusDto? Status { get; set; }

    private string? LoadError { get; set; }

    private FieldProblem? Problem { get; set; }

    private bool Busy { get; set; }

    private GenerationList? List { get; set; }

    private string? DisabledReasonId => Status?.State == GenerationAvailabilityDto.Ready ? null : "generation-status-reason";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        Status = await StatusSource.GetAsync();
        LoadError = await ApiCall.RunAsync(async () =>
        {
            Form = new SongForm(await Api.ListStylesAsync(), await Api.ListLyricsSheetsAsync());
            if (LyricsId is { } id)
            {
                Form.UseSheet(await Api.GetLyricsSheetAsync(id));
            }
        });
    }

    private async Task GenerateAsync()
    {
        Problem = Form!.Validate();
        if (Problem is null)
        {
            Busy = true;
            await RunAsync(async () => await List!.AddAsync(await Api.CreateSongAsync(Form.Request())));
            Busy = false;
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (ApiException ex)
        {
            Problem = FieldProblem.From(ex.Error);
        }
    }
}
