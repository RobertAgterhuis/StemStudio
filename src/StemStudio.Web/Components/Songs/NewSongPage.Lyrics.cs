using Microsoft.AspNetCore.Components.Forms;
using StemStudio.Contracts;
using StemStudio.Web.Api;
using StemStudio.Web.Songs;

namespace StemStudio.Web.Components.Songs;

/// <summary>The lyrics helpers of the song page: choose, upload, add sections and save (feature 010, FR-001..FR-003).</summary>
public sealed partial class NewSongPage
{
    /// <summary>Title of the sheet saved last.</summary>
    private string? SavedTitle { get; set; }

    private void SelectSheet(Guid? sheetId) => Form!.SelectSheet(sheetId);

    private Task ProposeAsync() => HelperAsync(async () => Form!.Text = (await Api.ProposeSectionsAsync(Form.Text)).Text);

    private Task SaveLyricsAsync() => HelperAsync(async () =>
    {
        var sheet = Form!.Sheet is { } current
            ? await Api.UpdateLyricsSheetAsync(current.Id, Form.LyricsInput())
            : await Api.CreateLyricsSheetAsync(Form.LyricsInput());
        Form.UseSheet(sheet);
        SavedTitle = sheet.Title;
    });

    private Task UploadAsync(InputFileChangeEventArgs args)
    {
        var file = args.File;
        Problem = LyricsFileRules.Problem(file.Name, file.Size) is { } problem ? new FieldProblem("file", problem) : null;
        return Problem is null ? HelperAsync(() => UploadFileAsync(file)) : Task.CompletedTask;
    }

    private async Task UploadFileAsync(IBrowserFile file)
    {
        await using var stream = file.OpenReadStream(ContractLimits.MaxLyricsUploadBytes);
        Form!.UseSheet(await Api.UploadLyricsSheetAsync(new UploadFile(stream, file.Name, file.ContentType), null, Form.Language));
    }

    private async Task HelperAsync(Func<Task> action)
    {
        Busy = true;
        Problem = null;
        await RunAsync(action);
        Busy = false;
    }
}
