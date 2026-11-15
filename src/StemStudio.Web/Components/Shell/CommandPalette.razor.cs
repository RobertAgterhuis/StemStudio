using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Web.Api;
using StemStudio.Web.Help;
using StemStudio.Web.Interop;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Shell;

/// <summary>One palette entry.</summary>
/// <param name="Id">Command id (ui-test-ids <c>command-&lt;id&gt;</c>).</param>
/// <param name="Label">Visible label.</param>
/// <param name="Group">Heading the entry is listed under.</param>
/// <param name="Run">What it does.</param>
public sealed record PaletteItem(string Id, string Label, string Group, Func<Task> Run);

/// <summary>The command palette dialog; loads tracks and mixes when opened (failures leave them out).</summary>
public sealed partial class CommandPalette
{
    private ElementReference _input;
    private IAsyncDisposable? _focus;
    private List<PaletteItem> _items = [];

    [Inject]
    private ShellOverlays Overlays { get; set; } = default!;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private HelpContext Help { get; set; } = default!;

    [Inject]
    private IHelpCatalog Catalog { get; set; } = default!;

    [Inject]
    private PageCommands Commands { get; set; } = default!;

    [Inject]
    private IWebInterop Web { get; set; } = default!;

    private string Query { get; set; } = string.Empty;

    private int Selected { get; set; }

    private List<PaletteItem> Visible =>
        _items.Where(i => i.Label.Contains(Query.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

    private string? ActiveId => Visible.ElementAtOrDefault(Selected) is { } item ? $"command-{item.Id}" : null;

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await (_focus?.DisposeAsync() ?? ValueTask.CompletedTask);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        _focus = await Web.CaptureFocusAsync();

        // Screens, actions and help show at once; tracks and mixes join when the API has answered.
        _items = [.. Screens(), .. Actions(), .. HelpItems()];
        _items = [.. Screens(), .. Actions(), .. await TracksAsync(), .. await MixesAsync(), .. HelpItems()];
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => firstRender ? _input.FocusAsync().AsTask() : Task.CompletedTask;

    private IEnumerable<PaletteItem> Screens() =>
    [
        new("go-import", "Go to Import", "Go to", () => GoAsync(string.Empty)),
        new("go-tracks", "Go to Tracks", "Go to", () => GoAsync("tracks")),
        new("go-mixes", "Go to Mixes", "Go to", () => GoAsync("mixes")),
    ];

    private IEnumerable<PaletteItem> Actions() => Commands.Items.Select(c => new PaletteItem(c.Id, c.Label, "Actions", c.Run));

    private IEnumerable<PaletteItem> HelpItems() =>
        Catalog.Topics.OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .Select(t => new PaletteItem($"help-{t.Id}", $"Help: {t.Title}", "Help", () => Help.ShowAsync(t.Id)));

    private async Task<IEnumerable<PaletteItem>> TracksAsync()
    {
        try
        {
            var tracks = await Api.ListTracksAsync();
            return tracks.Select(t => new PaletteItem($"track-{t.Id}", $"Track: {t.Title}", "Tracks", () => GoAsync($"tracks/{t.Id}")));
        }
        catch (ApiException)
        {
            return [];
        }
    }

    private async Task<IEnumerable<PaletteItem>> MixesAsync()
    {
        try
        {
            var mixes = await Api.ListMixesAsync();
            return mixes.Select(m => new PaletteItem($"mix-{m.Id}", $"Mix: {m.Name} ({m.TrackTitle})", "Mixes",
                () => GoAsync($"tracks/{m.TrackId}/mixer?mixId={m.Id}")));
        }
        catch (ApiException)
        {
            return [];
        }
    }

    private Task GoAsync(string path)
    {
        Navigation.NavigateTo(path);
        return Task.CompletedTask;
    }

    private void OnInput(ChangeEventArgs args)
    {
        Query = args.Value?.ToString() ?? string.Empty;
        Selected = 0;
    }

    private Task KeyAsync(KeyboardEventArgs args) => args.Key switch
    {
        "ArrowDown" => Move(1),
        "ArrowUp" => Move(-1),
        "Enter" => Visible.ElementAtOrDefault(Selected) is { } item ? RunAsync(item) : Task.CompletedTask,
        "Escape" => CloseAsync(),
        _ => Task.CompletedTask,
    };

    private Task Move(int delta)
    {
        Selected = Math.Clamp(Selected + delta, 0, Math.Max(Visible.Count - 1, 0));
        return Task.CompletedTask;
    }

    private async Task RunAsync(PaletteItem item)
    {
        await CloseAsync();
        await item.Run();
    }

    private Task CloseAsync()
    {
        Overlays.Close();
        return Task.CompletedTask;
    }
}
