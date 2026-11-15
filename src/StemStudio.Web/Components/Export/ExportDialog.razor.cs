using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Api;
using StemStudio.Web.Export;
using StemStudio.Web.Interop;

namespace StemStudio.Web.Components.Export;

/// <summary>Submits the export of an <see cref="ExportTarget"/> and confirms it (feature 007).</summary>
public sealed partial class ExportDialog : IAsyncDisposable
{
    private ExportForm _form = default!;
    private ElementReference _title;
    private IAsyncDisposable? _returnFocus;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IWebInterop Web { get; set; } = default!;

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
        _form = new ExportForm(Target);
        _returnFocus = await Web.CaptureFocusAsync();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await _title.FocusAsync();
        }
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
    private sealed record SavedExport(Guid FirstId, string Text)
    {
        public static SavedExport From(AssetDto asset) =>
            new(asset.Id, $"Saved \"{asset.Name}\" to the library. It is being prepared in the background.");

        public static SavedExport From(StemPackDto pack) =>
            new(pack.Items[0].Id, $"Saved {pack.Items.Count} items to the library, one per stem. They are being prepared in the background.");
    }
}
