using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Songs;

/// <summary>Loads the saved lyrics sheets and runs new, edit and delete (feature 010, US2).</summary>
public sealed partial class LyricsListPage
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    private List<LyricsSheetDto>? Items { get; set; }

    private string? LoadError { get; set; }

    private string? ActionError { get; set; }

    private string Search { get; set; } = string.Empty;

    /// <summary>The editor: open, with the sheet to edit (null = new lyrics).</summary>
    private (bool Open, LyricsSheetDto? Sheet) Editing { get; set; }

    private LyricsSheetDto? Deleting { get; set; }

    private List<LyricsSheetDto> Visible => [.. Items!.Where(Matches)];

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        (var sheets, LoadError) = await ApiCall.TryAsync(() => Api.ListLyricsSheetsAsync());
        Items = sheets is null ? null : [.. sheets];
    }

    private static string FirstLine(string text) => text.Split('\n', 2)[0];

    private bool Matches(LyricsSheetDto sheet) =>
        sheet.Title.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase) || sheet.Text.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase);

    private void OnSaved(LyricsSheetDto sheet)
    {
        Items!.RemoveAll(s => s.Id == sheet.Id);
        Items.Insert(0, sheet);
        Editing = (false, null);
    }

    private async Task DeleteAsync()
    {
        var sheet = Deleting!;
        Deleting = null;
        ActionError = await ApiCall.RunAsync(async () =>
        {
            await Api.DeleteLyricsSheetAsync(sheet.Id);
            Items!.RemoveAll(s => s.Id == sheet.Id);
        });
    }
}
