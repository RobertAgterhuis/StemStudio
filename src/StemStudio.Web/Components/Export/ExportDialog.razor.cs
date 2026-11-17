using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Api;
using StemStudio.Web.Export;

namespace StemStudio.Web.Components.Export;

/// <summary>Submits the export of an <see cref="ExportTarget"/> and confirms it (feature 007).</summary>
public sealed partial class ExportDialog
{
    private ExportForm _form = default!;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>What is exported.</summary>
    [Parameter, EditorRequired]
    public ExportTarget Target { get; set; } = default!;

    /// <summary>Cancel, Close or Escape.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    private string? Error { get; set; }

    private bool Busy { get; set; }

    private SavedExport? Saved { get; set; }

    private string Subject => Target.Kind switch
    {
        ExportKind.Mix => "What you hear in the mixer, or the channels you select, with their volume and pan.",
        ExportKind.StemPack => $"Every stem of {Target.Track.Title} as its own item, unprocessed.",
        _ => $"{ChannelLabel(Target.Channel!)} of {Target.Track.Title}, unprocessed (raw).",
    };

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        _form = new ExportForm(Target);
        await base.OnInitializedAsync();
    }

    private static string ChannelLabel(string channel) =>
        channel.Contains('.', StringComparison.Ordinal) ? $"{channel[(channel.IndexOf('.') + 1)..]} ({channel[..channel.IndexOf('.')]})" : channel;

    private Task OnKeyAsync(KeyboardEventArgs args) => args.Key == "Escape" ? OnClose.InvokeAsync() : Task.CompletedTask;

    private void OnIncludeDetail(ChangeEventArgs args) => _form.IncludeDetail = args.Value is true;

    private async Task SubmitAsync()
    {
        Error = _form.Validate();
        if (Error is null)
        {
            Busy = true;
            Saved = await TrySaveAsync();
            Busy = false;
        }
    }

    private async Task<SavedExport?> TrySaveAsync()
    {
        try
        {
            return Target.Kind == ExportKind.StemPack
                ? SavedExport.From(await Api.CreateStemPackAsync(_form.PackRequest()))
                : SavedExport.From(await Api.CreateAudioExportAsync(_form.AudioRequest()));
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
            return null;
        }
    }

    /// <summary>The confirmation of a submitted export.</summary>
    /// <param name="FirstId">The (first) created item, target of <c>export-saved-open</c>.</param>
    /// <param name="Text">The confirmation text.</param>
    /// <param name="Placement">Where the (first) item is filed (feature 022, FR-012); null for an API before 1.22.0.</param>
    private sealed record SavedExport(Guid FirstId, string Text, StemStudio.Contracts.Library.PlacementDto? Placement)
    {
        /// <summary>The link of <c>export-saved-open</c>: the item in its song folder (ui-test-ids 1.22.0).</summary>
        public string OpenUrl => StemStudio.Web.Library.LibraryLinks.Item(FirstId, Placement);

        public static SavedExport From(AssetDto asset) =>
            new(asset.Id, $"Saved \"{asset.Name}\" to the library. It is being prepared in the background.", asset.Placement);

        public static SavedExport From(StemPackDto pack) =>
            new(pack.Items[0].Id, $"Saved {pack.Items.Count} items to the library, one per stem. They are being prepared in the background.", pack.Items[0].Placement);
    }
}
